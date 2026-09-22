using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace AsteroidColony
{
    // The static registry is intentionally scene-local at runtime; the subsystem
    // reset below also covers play sessions with domain reload disabled.
    /// <summary>
    /// Authoring point for a physical connection between two module prefabs.
    ///
    /// Points register themselves while enabled. A single startup pass pairs the
    /// nearest available points and creates one runtime NavMeshLink per pair.
    /// Construction code can call DiscoverAndConnect again after it has placed a
    /// new module or moved an existing one.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Colony/Navigation/Module Connection Point")]
    public sealed class ModuleConnectionPoint : MonoBehaviour
    {
        private const float MinimumDistance = 0.0001f;
        private const float MinimumWidth = 0.0001f;

        [SerializeField]
        private NavMeshSurface ownerSurface;

        [SerializeField]
        private Transform walkAnchor;

        [SerializeField, Min(0.0001f)]
        private float maxPartnerDistance = 0.25f;

        [SerializeField, Min(0.0001f)]
        private float linkWidth = 0.5f;

        private static readonly List<ModuleConnectionPoint> s_RegisteredPoints =
            new List<ModuleConnectionPoint>();
        private static readonly List<Connection> s_Connections = new List<Connection>();
        private static readonly HashSet<string> s_AmbiguityWarnings =
            new HashSet<string>(StringComparer.Ordinal);

        private static bool s_StartupDiscoveryHasRun;

        private Connection connection;

        /// <summary>The surface that owns this module's baked NavMesh.</summary>
        public NavMeshSurface OwnerSurface
        {
            get { return ownerSurface; }
            set { ownerSurface = value; }
        }

        /// <summary>The point inside this module where a link should meet the NavMesh.</summary>
        public Transform WalkAnchor
        {
            get { return walkAnchor; }
            set { walkAnchor = value; }
        }

        /// <summary>Maximum node-to-node distance accepted for this point.</summary>
        public float MaxPartnerDistance
        {
            get { return maxPartnerDistance; }
            set { maxPartnerDistance = Mathf.Max(MinimumDistance, value); }
        }

        /// <summary>Width of the connection at either link endpoint.</summary>
        public float LinkWidth
        {
            get { return linkWidth; }
            set { linkWidth = Mathf.Max(MinimumWidth, value); }
        }

        /// <summary>The point currently paired with this point, or null.</summary>
        public ModuleConnectionPoint CurrentPartner
        {
            get { return connection == null ? null : connection.Other(this); }
        }

        /// <summary>The runtime link owned by the current connection, or null.</summary>
        public NavMeshLink CurrentLink
        {
            get { return connection == null ? null : connection.Link; }
        }

        public bool IsConnected
        {
            get { return connection != null && !connection.IsDisposed; }
        }

        /// <summary>
        /// Clears static registration and connection state at the beginning of a
        /// play session. This is also required when domain reload is disabled.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            for (int i = s_Connections.Count - 1; i >= 0; i--)
                s_Connections[i].Dispose();

            s_Connections.Clear();
            s_RegisteredPoints.Clear();
            s_AmbiguityWarnings.Clear();
            s_StartupDiscoveryHasRun = false;
        }

        private void Awake()
        {
            ResolveOwnerSurface(false);
        }

        private void OnEnable()
        {
            ResolveOwnerSurface(false);
            Register(this);
        }

        private void Start()
        {
            RunStartupDiscoveryOnce();
        }

        private void OnDisable()
        {
            Disconnect();
            Unregister(this);
        }

        private void OnDestroy()
        {
            Disconnect();
            Unregister(this);
        }

        private void OnValidate()
        {
            maxPartnerDistance = Mathf.Max(MinimumDistance, maxPartnerDistance);
            linkWidth = Mathf.Max(MinimumWidth, linkWidth);
            ResolveOwnerSurface(false);
        }

        /// <summary>
        /// Runs the scene's one-time startup pass. OnEnable has already registered
        /// all scene objects before any Start method is called.
        /// </summary>
        public static int RunStartupDiscoveryOnce()
        {
            if (s_StartupDiscoveryHasRun)
                return 0;

            s_StartupDiscoveryHasRun = true;
            return DiscoverAndConnect();
        }

        /// <summary>
        /// Pairs currently registered, unpaired points and creates their links.
        /// Existing valid connections are preserved. This is the entry point for
        /// future snap-building code as well as the initial static scene.
        /// </summary>
        public static int DiscoverAndConnect()
        {
            PruneRegistration();

            List<ModuleConnectionPoint> candidates = new List<ModuleConnectionPoint>();
            for (int i = 0; i < s_RegisteredPoints.Count; i++)
            {
                ModuleConnectionPoint point = s_RegisteredPoints[i];
                if (point == null || !point.isActiveAndEnabled || point.IsConnected)
                    continue;

                string error;
                if (!point.TryGetConfigurationError(out error))
                {
                    Debug.LogWarning(error, point);
                    continue;
                }

                candidates.Add(point);
            }

            List<CandidatePair> pairs = new List<CandidatePair>();
            for (int i = 0; i < candidates.Count; i++)
            {
                ModuleConnectionPoint a = candidates[i];
                for (int j = i + 1; j < candidates.Count; j++)
                {
                    ModuleConnectionPoint b = candidates[j];
                    if (a.ownerSurface == b.ownerSurface)
                        continue;

                    float distanceSquared = (a.transform.position - b.transform.position).sqrMagnitude;
                    float maxDistance = Mathf.Min(a.maxPartnerDistance, b.maxPartnerDistance);
                    if (distanceSquared > maxDistance * maxDistance)
                        continue;

                    CandidatePair pair = new CandidatePair(a, b, distanceSquared);
                    pairs.Add(pair);
                }
            }

            WarnAboutAmbiguousCandidates(candidates, pairs);
            pairs.Sort(CandidatePairComparer.Instance);

            int connectedCount = 0;
            for (int i = 0; i < pairs.Count; i++)
            {
                CandidatePair pair = pairs[i];
                if (pair.A == null || pair.B == null || pair.A.IsConnected || pair.B.IsConnected)
                    continue;

                if (TryConnect(pair.A, pair.B))
                    connectedCount++;
            }

            return connectedCount;
        }

        /// <summary>Removes this point's connection and destroys its owned runtime link.</summary>
        public void Disconnect()
        {
            if (connection != null)
                connection.Dispose();
        }

        /// <summary>Returns a deterministic scene/hierarchy key used for tie breaking.</summary>
        public string GetStableHierarchyKey()
        {
            string sceneKey = gameObject.scene.path;
            if (string.IsNullOrEmpty(sceneKey))
                sceneKey = gameObject.scene.name;

            if (string.IsNullOrEmpty(sceneKey))
                sceneKey = "<unsaved-scene>";

            List<int> siblingIndexes = new List<int>();
            Transform current = transform;
            while (current != null)
            {
                siblingIndexes.Add(current.GetSiblingIndex());
                current = current.parent;
            }

            siblingIndexes.Reverse();
            return sceneKey + "/" + string.Join("/", siblingIndexes) + "/" + gameObject.name;
        }

        internal bool TryGetConfigurationError(out string error)
        {
            ResolveOwnerSurface(false);

            if (ownerSurface == null)
            {
                error = name + " has no owning NavMeshSurface. Assign the module root's surface.";
                return false;
            }

            if (!ownerSurface.isActiveAndEnabled)
            {
                error = name + " is owned by a disabled NavMeshSurface.";
                return false;
            }

            if (walkAnchor == null)
            {
                error = name + " has no WalkAnchor.";
                return false;
            }

            if (!walkAnchor.gameObject.activeInHierarchy)
            {
                error = name + " has an inactive WalkAnchor.";
                return false;
            }

            if (maxPartnerDistance < MinimumDistance)
            {
                error = name + " has a nonpositive maximum partner distance.";
                return false;
            }

            if (linkWidth < MinimumWidth)
            {
                error = name + " has a nonpositive link width.";
                return false;
            }

            error = null;
            return true;
        }

        private void ResolveOwnerSurface(bool log)
        {
            if (ownerSurface != null)
            {
                if (transform != ownerSurface.transform && !transform.IsChildOf(ownerSurface.transform))
                {
                    if (log)
                        Debug.LogError(name + " references a NavMeshSurface outside its module hierarchy.", this);
                }

                return;
            }

            NavMeshSurface[] surfaces = GetComponentsInParent<NavMeshSurface>(true);
            if (surfaces.Length == 1)
            {
                ownerSurface = surfaces[0];
            }
            else if (surfaces.Length > 1 && log)
            {
                Debug.LogError(
                    name + " is under multiple NavMeshSurfaces. Assign the module root surface explicitly.",
                    this);
            }
        }

        private static void Register(ModuleConnectionPoint point)
        {
            if (point != null && !s_RegisteredPoints.Contains(point))
                s_RegisteredPoints.Add(point);
        }

        private static void Unregister(ModuleConnectionPoint point)
        {
            s_RegisteredPoints.Remove(point);
        }

        private static void PruneRegistration()
        {
            for (int i = s_RegisteredPoints.Count - 1; i >= 0; i--)
            {
                ModuleConnectionPoint point = s_RegisteredPoints[i];
                if (point == null)
                    s_RegisteredPoints.RemoveAt(i);
            }
        }

        private static bool TryConnect(ModuleConnectionPoint a, ModuleConnectionPoint b)
        {
            string aError;
            string bError;
            if (!a.TryGetConfigurationError(out aError))
            {
                Debug.LogWarning(aError, a);
                return false;
            }

            if (!b.TryGetConfigurationError(out bError))
            {
                Debug.LogWarning(bError, b);
                return false;
            }

            if (a.ownerSurface == b.ownerSurface)
                return false;

            if (a.ownerSurface.agentTypeID != b.ownerSurface.agentTypeID)
            {
                Debug.LogWarning(
                    "Cannot connect " + a.name + " and " + b.name + " because their NavMesh agent types differ.",
                    a);
                return false;
            }

            float distanceSquared = (a.transform.position - b.transform.position).sqrMagnitude;
            float maxDistance = Mathf.Min(a.maxPartnerDistance, b.maxPartnerDistance);
            if (distanceSquared > maxDistance * maxDistance)
                return false;

            GameObject linkObject = new GameObject("ModuleNavMeshLink_" + a.name + "_" + b.name);
            linkObject.hideFlags = HideFlags.HideAndDontSave;
            linkObject.transform.position = (a.walkAnchor.position + b.walkAnchor.position) * 0.5f;

            NavMeshLink link = linkObject.AddComponent<NavMeshLink>();
            link.agentTypeID = a.ownerSurface.agentTypeID;
            link.startTransform = a.walkAnchor;
            link.endTransform = b.walkAnchor;
            link.width = Mathf.Min(a.linkWidth, b.linkWidth);
            link.bidirectional = true;
            link.area = NavMesh.GetAreaFromName("Walkable");
            if (link.area < 0)
                link.area = 0;
            link.autoUpdate = true;
            link.activated = true;

            Connection newConnection = new Connection(a, b, link, linkObject);
            a.connection = newConnection;
            b.connection = newConnection;
            s_Connections.Add(newConnection);
            return true;
        }

        private static void WarnAboutAmbiguousCandidates(
            List<ModuleConnectionPoint> points,
            List<CandidatePair> pairs)
        {
            Dictionary<ModuleConnectionPoint, List<ModuleConnectionPoint>> nearby =
                new Dictionary<ModuleConnectionPoint, List<ModuleConnectionPoint>>();

            for (int i = 0; i < pairs.Count; i++)
            {
                CandidatePair pair = pairs[i];
                AddNearby(nearby, pair.A, pair.B);
                AddNearby(nearby, pair.B, pair.A);
            }

            for (int i = 0; i < points.Count; i++)
            {
                ModuleConnectionPoint point = points[i];
                List<ModuleConnectionPoint> alternatives;
                if (!nearby.TryGetValue(point, out alternatives) || alternatives.Count < 2)
                    continue;

                string warningKey = point.GetStableHierarchyKey();
                if (s_AmbiguityWarnings.Add(warningKey))
                {
                    Debug.LogWarning(
                        point.name + " has " + alternatives.Count +
                        " nearby connection candidates. Nearest-available pairing will claim one; " +
                        "check the static layout if this is unexpected.",
                        point);
                }
            }
        }

        private static void AddNearby(
            Dictionary<ModuleConnectionPoint, List<ModuleConnectionPoint>> nearby,
            ModuleConnectionPoint point,
            ModuleConnectionPoint alternative)
        {
            List<ModuleConnectionPoint> alternatives;
            if (!nearby.TryGetValue(point, out alternatives))
            {
                alternatives = new List<ModuleConnectionPoint>();
                nearby.Add(point, alternatives);
            }

            if (!alternatives.Contains(alternative))
                alternatives.Add(alternative);
        }

        private sealed class Connection
        {
            private readonly ModuleConnectionPoint a;
            private readonly ModuleConnectionPoint b;
            private readonly NavMeshLink link;
            private readonly GameObject linkObject;
            private bool disposed;

            public Connection(
                ModuleConnectionPoint a,
                ModuleConnectionPoint b,
                NavMeshLink link,
                GameObject linkObject)
            {
                this.a = a;
                this.b = b;
                this.link = link;
                this.linkObject = linkObject;
            }

            public NavMeshLink Link
            {
                get { return link; }
            }

            public bool IsDisposed
            {
                get { return disposed; }
            }

            public ModuleConnectionPoint Other(ModuleConnectionPoint point)
            {
                if (point == a)
                    return b;
                if (point == b)
                    return a;
                return null;
            }

            public void Dispose()
            {
                if (disposed)
                    return;

                disposed = true;
                if (a != null && a.connection == this)
                    a.connection = null;
                if (b != null && b.connection == this)
                    b.connection = null;

                s_Connections.Remove(this);
                if (linkObject != null)
                {
                    if (Application.isPlaying)
                        UnityEngine.Object.Destroy(linkObject);
                    else
                        UnityEngine.Object.DestroyImmediate(linkObject);
                }
            }
        }

        private readonly struct CandidatePair
        {
            public readonly ModuleConnectionPoint A;
            public readonly ModuleConnectionPoint B;
            public readonly float DistanceSquared;
            public readonly string FirstKey;
            public readonly string SecondKey;

            public CandidatePair(ModuleConnectionPoint a, ModuleConnectionPoint b, float distanceSquared)
            {
                if (string.CompareOrdinal(a.GetStableHierarchyKey(), b.GetStableHierarchyKey()) <= 0)
                {
                    A = a;
                    B = b;
                }
                else
                {
                    A = b;
                    B = a;
                }

                DistanceSquared = distanceSquared;
                FirstKey = A.GetStableHierarchyKey();
                SecondKey = B.GetStableHierarchyKey();
            }
        }

        private sealed class CandidatePairComparer : IComparer<CandidatePair>
        {
            public static readonly CandidatePairComparer Instance = new CandidatePairComparer();

            public int Compare(CandidatePair x, CandidatePair y)
            {
                int distanceComparison = x.DistanceSquared.CompareTo(y.DistanceSquared);
                if (distanceComparison != 0)
                    return distanceComparison;

                int firstComparison = string.CompareOrdinal(x.FirstKey, y.FirstKey);
                if (firstComparison != 0)
                    return firstComparison;

                return string.CompareOrdinal(x.SecondKey, y.SecondKey);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.75f);
            Gizmos.DrawWireSphere(transform.position, maxPartnerDistance);

            if (walkAnchor != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(walkAnchor.position, 0.08f);
                Gizmos.DrawLine(transform.position, walkAnchor.position);
            }

            if (CurrentPartner != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, CurrentPartner.transform.position);
            }
        }
    }
}
