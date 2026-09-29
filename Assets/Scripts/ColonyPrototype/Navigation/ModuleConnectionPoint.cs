using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace AsteroidColony
{
    /// <summary>Small graph model used to derive contiguous walking regions.</summary>
    public sealed class ModuleRegionGraph
    {
        private readonly HashSet<string> modules = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> connections = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> regions =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private bool dirty = true;

        public int Revision { get; private set; }
        public int ModuleCount => modules.Count;

        public void Clear()
        {
            modules.Clear();
            connections.Clear();
            regions.Clear();
            dirty = true;
            Revision = 0;
        }

        public void RegisterModule(string moduleId)
        {
            if (!string.IsNullOrWhiteSpace(moduleId) && modules.Add(moduleId))
                MarkChanged();
        }

        public void UnregisterModule(string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId) || !modules.Remove(moduleId))
                return;
            connections.RemoveWhere(edge => EdgeContains(edge, moduleId));
            MarkChanged();
        }

        public void SetConnection(string firstModuleId, string secondModuleId, bool connected)
        {
            if (string.IsNullOrWhiteSpace(firstModuleId) ||
                string.IsNullOrWhiteSpace(secondModuleId) ||
                string.Equals(firstModuleId, secondModuleId, StringComparison.Ordinal))
                return;
            string edge = MakeEdge(firstModuleId, secondModuleId);
            bool changed = connected ? connections.Add(edge) : connections.Remove(edge);
            if (!changed)
                return;
            if (connected)
            {
                modules.Add(firstModuleId);
                modules.Add(secondModuleId);
            }
            MarkChanged();
        }

        public bool TryGetRegion(string moduleId, out int region)
        {
            EnsureRegions();
            return regions.TryGetValue(moduleId, out region);
        }

        public void NotifyNavigationRebuilt() => MarkChanged();

        private void MarkChanged()
        {
            Revision++;
            dirty = true;
        }

        private void EnsureRegions()
        {
            if (!dirty)
                return;
            regions.Clear();
            List<string> orderedModules = new List<string>(modules);
            orderedModules.Sort(StringComparer.Ordinal);
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            Queue<string> pending = new Queue<string>();
            int regionId = 0;
            for (int index = 0; index < orderedModules.Count; index++)
            {
                string root = orderedModules[index];
                if (!visited.Add(root))
                    continue;
                pending.Enqueue(root);
                while (pending.Count > 0)
                {
                    string current = pending.Dequeue();
                    regions[current] = regionId;
                    foreach (string edge in connections)
                    {
                        string neighbor = EdgeOther(edge, current);
                        if (neighbor != null && modules.Contains(neighbor) && visited.Add(neighbor))
                            pending.Enqueue(neighbor);
                    }
                }
                regionId++;
            }
            dirty = false;
        }

        private static string MakeEdge(string a, string b) =>
            string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;

        private static bool EdgeContains(string edge, string id) =>
            edge.StartsWith(id + "|", StringComparison.Ordinal) ||
            edge.EndsWith("|" + id, StringComparison.Ordinal);

        private static string EdgeOther(string edge, string id)
        {
            int separator = edge.IndexOf('|');
            if (separator < 0)
                return null;
            string first = edge.Substring(0, separator);
            string second = edge.Substring(separator + 1);
            if (string.Equals(first, id, StringComparison.Ordinal))
                return second;
            return string.Equals(second, id, StringComparison.Ordinal) ? first : null;
        }
    }

    /// <summary>
    /// Derived runtime view of which authored module surfaces are connected by
    /// walking links. ModuleConnectionPoint remains the owner of link creation.
    /// Region values are transient diagnostics and are never saved to gameplay data.
    /// </summary>
    public static class ModuleNavigationTopology
    {
        private static readonly Dictionary<string, NavMeshSurface> modules =
            new Dictionary<string, NavMeshSurface>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> moduleReferenceCounts =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly ModuleRegionGraph graph = new ModuleRegionGraph();

        public static int Revision => graph.Revision;
        public static int ModuleCount => modules.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            modules.Clear();
            moduleReferenceCounts.Clear();
            graph.Clear();
        }

        internal static void RegisterPoint(NavMeshSurface surface)
        {
            if (!Application.isPlaying || surface == null)
                return;
            string id = surface.GetEntityId().ToString();
            modules[id] = surface;
            moduleReferenceCounts.TryGetValue(id, out int count);
            moduleReferenceCounts[id] = count + 1;
            graph.RegisterModule(id);
        }

        internal static void UnregisterPoint(NavMeshSurface surface)
        {
            if (!Application.isPlaying || surface == null)
                return;
            string id = surface.GetEntityId().ToString();
            if (!moduleReferenceCounts.TryGetValue(id, out int count))
                return;
            if (count > 1)
            {
                moduleReferenceCounts[id] = count - 1;
                return;
            }
            moduleReferenceCounts.Remove(id);
            modules.Remove(id);
            graph.UnregisterModule(id);
        }

        internal static void SetConnection(
            NavMeshSurface first,
            NavMeshSurface second,
            bool connected)
        {
            if (!Application.isPlaying || first == null || second == null || first == second)
                return;
            string a = first.GetEntityId().ToString();
            string b = second.GetEntityId().ToString();
            graph.SetConnection(a, b, connected);
            if (connected)
            {
                modules[a] = first;
                modules[b] = second;
            }
        }

        /// <summary>Call after a runtime NavMesh rebuild changes walkability.</summary>
        public static void NotifyNavigationRebuilt()
        {
            if (Application.isPlaying)
            {
                ModuleConnectionPoint.RefreshConnectionReadiness();
                graph.NotifyNavigationRebuilt();
            }
        }

        /// <summary>Resolves a region from an anchor under its owning module surface.</summary>
        public static bool TryGetRegion(Transform anchor, out int region)
        {
            region = -1;
            if (anchor == null)
                return false;
            NavMeshSurface surface = anchor.GetComponentInParent<NavMeshSurface>(true);
            if (surface == null)
                return false;
            string id = surface.GetEntityId().ToString();
            if (!modules.ContainsKey(id))
                return false;
            return graph.TryGetRegion(id, out region);
        }

        /// <summary>Returns whether two registered anchors share a walking region.</summary>
        public static bool AreInSameRegion(Transform first, Transform second)
        {
            if (!TryGetRegion(first, out int firstRegion) ||
                !TryGetRegion(second, out int secondRegion))
                return true; // Unknown membership must not fabricate a disconnection.
            return firstRegion == secondRegion;
        }

    }

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
        private const float WalkAnchorSnapDistance = 1.5f;

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
            set
            {
                if (ownerSurface == value)
                    return;
                NavMeshSurface previous = ownerSurface;
                bool registered = s_RegisteredPoints.Contains(this);
                if (connection != null)
                    Disconnect();
                if (registered)
                    ModuleNavigationTopology.UnregisterPoint(previous);
                ownerSurface = value;
                if (registered)
                    ModuleNavigationTopology.RegisterPoint(ownerSurface);
            }
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
            ModuleNavigationTopology.RegisterPoint(ownerSurface);
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

            Debug.Log(
                "[B2Nav] Module connection discovery: registered=" + s_RegisteredPoints.Count +
                ", candidates=" + candidates.Count +
                ", nearbyPairs=" + pairs.Count +
                ", connected=" + connectedCount + ".");

            return connectedCount;
        }

        /// <summary>
        /// Rebuilds links after a runtime builder relocates a module. The builder
        /// calls this once after applying transforms, rather than asking logistics
        /// to infer changed station geometry.
        /// </summary>
        public static int NotifyModuleMoved(Transform moduleRoot)
        {
            if (moduleRoot == null)
                return 0;
            for (int index = 0; index < s_RegisteredPoints.Count; index++)
            {
                ModuleConnectionPoint point = s_RegisteredPoints[index];
                if (point != null && (point.transform == moduleRoot ||
                    point.transform.IsChildOf(moduleRoot)))
                    point.Disconnect();
            }
            ModuleNavigationTopology.NotifyNavigationRebuilt();
            return DiscoverAndConnect();
        }

        /// <summary>
        /// Called by a runtime module builder after a new module has been placed
        /// and its navigation data is ready. OnEnable only registers the module;
        /// it does not discover links while a builder may still be positioning it.
        /// </summary>
        public static int NotifyModuleAdded(Transform moduleRoot)
        {
            if (moduleRoot == null)
                return 0;
            ModuleNavigationTopology.NotifyNavigationRebuilt();
            return DiscoverAndConnect();
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

            if (ownerSurface.navMeshData == null)
            {
                error = name + " is owned by a NavMeshSurface with no baked NavMeshData.";
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
            if (point != null && s_RegisteredPoints.Remove(point))
                ModuleNavigationTopology.UnregisterPoint(point.ownerSurface);
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

            NavMeshHit aHit;
            NavMeshHit bHit;
            if (!TrySampleWalkableEndpoint(a, out aHit) ||
                !TrySampleWalkableEndpoint(b, out bHit))
            {
                return false;
            }

            Debug.Log(
                "[B2Nav] Connecting candidate pair " + a.GetStableHierarchyKey() +
                " <-> " + b.GetStableHierarchyKey() +
                "; nodeDistance=" + Mathf.Sqrt(distanceSquared).ToString("F3") +
                "; walkA=" + aHit.position +
                "; walkB=" + bHit.position +
                "; agentType=" + a.ownerSurface.agentTypeID + ".");

            // Authored anchors define the link. Sampling above is diagnostic only:
            // discovery must never rewrite module geometry or anchor transforms.

            GameObject linkObject = new GameObject("ModuleNavMeshLink_" + a.name + "_" + b.name);
            linkObject.hideFlags = HideFlags.HideAndDontSave;
            linkObject.transform.position = (aHit.position + bHit.position) * 0.5f;

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

            NavMeshPath connectionPath = new NavMeshPath();
            NavMeshQueryFilter filter = CreateWalkableFilter(a.ownerSurface.agentTypeID);
            bool navigationReady = NavMesh.CalculatePath(
                    aHit.position, bHit.position, filter, connectionPath) &&
                connectionPath.status == NavMeshPathStatus.PathComplete;
            if (!navigationReady)
            {
                Debug.LogWarning(
                    "Created a NavMeshLink between " + a.name + " and " + b.name +
                    ", but the link is not yet walkable. The topology registry will keep " +
                    "these modules separate until a complete path is available.",
                    a);
            }

            Connection newConnection = new Connection(a, b, link, linkObject, navigationReady);
            a.connection = newConnection;
            b.connection = newConnection;
            s_Connections.Add(newConnection);
            ModuleNavigationTopology.SetConnection(a.ownerSurface, b.ownerSurface, navigationReady);

            LogConnectionGeometry(a, b, "created");
            if (Application.isPlaying)
                a.StartCoroutine(LogConnectionNextFrame(a, b, newConnection));

            Debug.Log(
                "[B2Nav] Module connection link created: " + a.GetStableHierarchyKey() +
                " <-> " + b.GetStableHierarchyKey() +
                "; linkObject=" + linkObject.name +
                "; linkEnabled=" + link.enabled +
                "; activated=" + link.activated +
                "; topologyReady=" + navigationReady +
                "; endpointPathStatus=" + connectionPath.status +
                "; endpointPathCorners=" + connectionPath.corners.Length + ".",
                link);
            return true;
        }

        private static System.Collections.IEnumerator LogConnectionNextFrame(
            ModuleConnectionPoint a, ModuleConnectionPoint b, Connection connection)
        {
            yield return null;
            if (a != null && b != null && a.CurrentPartner == b)
            {
                connection.RefreshReadiness();
                LogConnectionGeometry(a, b, "next-frame");
            }
        }

        internal static void RefreshConnectionReadiness()
        {
            for (int index = 0; index < s_Connections.Count; index++)
                s_Connections[index]?.RefreshReadiness();
        }

        private static void LogConnectionGeometry(ModuleConnectionPoint a,
            ModuleConnectionPoint b, string phase)
        {
            NavMeshQueryFilter filter = CreateWalkableFilter(a.ownerSurface.agentTypeID);
            bool sampledA = NavMesh.SamplePosition(a.walkAnchor.position, out NavMeshHit hitA,
                WalkAnchorSnapDistance, filter);
            bool sampledB = NavMesh.SamplePosition(b.walkAnchor.position, out NavMeshHit hitB,
                WalkAnchorSnapDistance, filter);
            NavMeshPath path = new NavMeshPath();
            bool calculated = sampledA && sampledB &&
                NavMesh.CalculatePath(hitA.position, hitB.position, filter, path);
            List<string> corners = new List<string>();
            foreach (Vector3 corner in path.corners)
                corners.Add(corner.ToString("F4"));
            Debug.Log("[B2LinkGeometry] phase=" + phase +
                "; A=" + a.ownerSurface.name + "/" + a.name + "/" + a.walkAnchor.name +
                "; authoredA=" + a.walkAnchor.position.ToString("F4") +
                "; localA=" + a.walkAnchor.localPosition.ToString("F4") +
                "; sampledA=" + sampledA + ":" + hitA.position.ToString("F4") +
                "; sampleDistanceA=" + Vector3.Distance(a.walkAnchor.position, hitA.position) +
                "; B=" + b.ownerSurface.name + "/" + b.name + "/" + b.walkAnchor.name +
                "; authoredB=" + b.walkAnchor.position.ToString("F4") +
                "; localB=" + b.walkAnchor.localPosition.ToString("F4") +
                "; sampledB=" + sampledB + ":" + hitB.position.ToString("F4") +
                "; sampleDistanceB=" + Vector3.Distance(b.walkAnchor.position, hitB.position) +
                "; calculated=" + calculated + "; status=" + path.status +
                "; corners=" + string.Join(" -> ", corners), a);
        }

        private static bool TrySampleWalkableEndpoint(
            ModuleConnectionPoint point,
            out NavMeshHit hit)
        {
            NavMeshQueryFilter filter = CreateWalkableFilter(point.ownerSurface.agentTypeID);
            if (NavMesh.SamplePosition(
                    point.walkAnchor.position,
                    out hit,
                    WalkAnchorSnapDistance,
                    filter))
            {
                return true;
            }

            Debug.LogWarning(
                "Cannot connect " + point.name + ": its WalkAnchor at " +
                point.walkAnchor.position + " is more than " +
                WalkAnchorSnapDistance + " units from a Walkable NavMesh for agent type " +
                point.ownerSurface.agentTypeID + ". Check the module bake and anchor position.",
                point);
            return false;
        }

        private static NavMeshQueryFilter CreateWalkableFilter(int agentTypeID)
        {
            int walkableArea = NavMesh.GetAreaFromName("Walkable");
            return new NavMeshQueryFilter
            {
                agentTypeID = agentTypeID,
                areaMask = walkableArea >= 0 ? 1 << walkableArea : NavMesh.AllAreas
            };
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
            private bool navigationReady;

            public Connection(
                ModuleConnectionPoint a,
                ModuleConnectionPoint b,
                NavMeshLink link,
                GameObject linkObject,
                bool navigationReady)
            {
                this.a = a;
                this.b = b;
                this.link = link;
                this.linkObject = linkObject;
                this.navigationReady = navigationReady;
            }

            public NavMeshLink Link
            {
                get { return link; }
            }

            public bool IsDisposed
            {
                get { return disposed; }
            }

            public bool RefreshReadiness()
            {
                bool ready = !disposed && a != null && b != null && link != null &&
                    link.isActiveAndEnabled && link.activated && HasCompletePath(a, b);
                if (ready != navigationReady)
                {
                    navigationReady = ready;
                    ModuleNavigationTopology.SetConnection(
                        a != null ? a.ownerSurface : null,
                        b != null ? b.ownerSurface : null,
                        navigationReady);
                }
                return navigationReady;
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
                ModuleNavigationTopology.SetConnection(
                    a != null ? a.ownerSurface : null,
                    b != null ? b.ownerSurface : null,
                    false);
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

            private static bool HasCompletePath(
                ModuleConnectionPoint first,
                ModuleConnectionPoint second)
            {
                if (first == null || second == null || first.ownerSurface == null ||
                    second.ownerSurface == null || first.walkAnchor == null ||
                    second.walkAnchor == null ||
                    first.ownerSurface.agentTypeID != second.ownerSurface.agentTypeID)
                    return false;

                NavMeshQueryFilter filter = CreateWalkableFilter(first.ownerSurface.agentTypeID);
                if (!NavMesh.SamplePosition(first.walkAnchor.position, out NavMeshHit firstHit,
                        WalkAnchorSnapDistance, filter) ||
                    !NavMesh.SamplePosition(second.walkAnchor.position, out NavMeshHit secondHit,
                        WalkAnchorSnapDistance, filter))
                    return false;

                NavMeshPath path = new NavMeshPath();
                return NavMesh.CalculatePath(firstHit.position, secondHit.position, filter, path) &&
                    path.status == NavMeshPathStatus.PathComplete;
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
