using System.Diagnostics;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AsteroidColony
{
    public enum ShuttleRouteKind { Direct, Visibility }
    public enum ShuttleRouteFailure { None, NoRouteWithinBudget, TooManyRelevantObstacles, GraphDisconnected, InvalidObstacleGeometry }

    public sealed class ShuttleRoutePlan
    {
        public ShuttleRouteKind kind;
        public ShuttleRouteFailure failure;
        public string diagnostic;
        public readonly List<FlightWaypoint> cruiseWaypoints = new List<FlightWaypoint>();
        public int relevantObstacleCount;
        public int candidateNodeCount;
        public int edgeSweepCount;
        public int graphExpansionCount;
        public float routeLength;
        public float straightLineDistance;
        public float routeLengthRatio;
        public double planningMilliseconds;
    }

    /// <summary>Plans the strategic cruise segment; docking clearance and capture remain voyage-owned.</summary>
    [DisallowMultipleComponent]
    public sealed class ShuttleRoutePlanner : MonoBehaviour
    {
        [SerializeField] private bool showDebugGizmos;
        private readonly List<Vector3> debugCandidates = new List<Vector3>();
        private readonly List<Vector3> debugGraphNodes = new List<Vector3>();
        private readonly List<Vector3> debugRawPath = new List<Vector3>();
        private readonly List<Vector3> debugSimplifiedPath = new List<Vector3>();
        private readonly List<Vector2Int> debugEdges = new List<Vector2Int>();
        private readonly List<float> debugPassSpeeds = new List<float>();

        public bool ShowDebugGizmos { get => showDebugGizmos; set => showDebugGizmos = value; }

        public ShuttleRoutePlan Plan(Vector3 start, Vector3 goal, ShuttleNavigationProfile profile,
            float maxCruiseSpeed, Transform ignoredRoot)
        {
            Stopwatch timer = Stopwatch.StartNew();
            ShuttleRoutePlan result = PlanInternal(start, goal, profile, maxCruiseSpeed, ignoredRoot);
            timer.Stop();
            result.planningMilliseconds = timer.Elapsed.TotalMilliseconds;
            result.straightLineDistance = Vector3.Distance(start, goal);
            if (result.failure == ShuttleRouteFailure.None)
            {
                Vector3 previous = start;
                for (int i = 0; i < result.cruiseWaypoints.Count; i++)
                {
                    Vector3 next = result.cruiseWaypoints[i].worldPosition;
                    result.routeLength += Vector3.Distance(previous, next);
                    previous = next;
                }
                result.routeLength += Vector3.Distance(previous, goal);
                result.routeLengthRatio = result.straightLineDistance > 0.0001f
                    ? result.routeLength / result.straightLineDistance : 1f;
            }
            return result;
        }

        private ShuttleRoutePlan PlanInternal(Vector3 start, Vector3 goal,
            ShuttleNavigationProfile profile, float maxCruiseSpeed, Transform ignoredRoot)
        {
            ShuttleRoutePlan result = new ShuttleRoutePlan();
            ClearDebug();
            if (profile == null || !ShuttleFlightIntegrator.IsFinite(start) || !ShuttleFlightIntegrator.IsFinite(goal))
                return Fail(result, ShuttleRouteFailure.InvalidObstacleGeometry, "missing profile or non-finite route endpoint");
            float radius = profile.SweptRadius;
            int layers = profile.navigationObstacleLayers.value;
            if (SpaceClearanceQuery.IsSweptSegmentClear(start, goal, radius, ignoredRoot, out _, layers))
            {
                result.kind = ShuttleRouteKind.Direct;
                result.diagnostic = "direct swept route is clear";
                result.edgeSweepCount = 1;
                debugRawPath.Add(start); debugRawPath.Add(goal);
                debugSimplifiedPath.Add(start); debugSimplifiedPath.Add(goal);
                return result;
            }

            List<SpaceNavigationObstacle> relevant = new List<SpaceNavigationObstacle>();
            SpaceNavigationObstacle first = SpaceClearanceQuery.FindFirstBlocker(start, goal, radius, ignoredRoot, layers);
            if (first == null)
                return Fail(result, ShuttleRouteFailure.InvalidObstacleGeometry,
                    "physics sweep was blocked but no marked strategic obstacle was identified");
            relevant.Add(first);
            result.relevantObstacleCount = relevant.Count;
            List<SpaceNavigationObstacle> discoveries = new List<SpaceNavigationObstacle>();
            List<Vector3> nodes = new List<Vector3>();
            List<List<int>> edges = new List<List<int>>();
            int edgeBudget = Mathf.Max(64, profile.maxEdgeTests);
            bool budgetExceeded = false;

            for (int round = 0; round < profile.maxExpansionRounds; round++)
            {
                result.graphExpansionCount = round + 1;
                for (int i = 0; i < relevant.Count; i++)
                    if (!ShuttleFlightIntegrator.IsFinite(relevant[i].ConservativeCenter) ||
                        !ShuttleFlightIntegrator.IsFinite(relevant[i].AvoidanceRadius) ||
                        relevant[i].AvoidanceRadius <= 0f)
                        return Fail(result, ShuttleRouteFailure.InvalidObstacleGeometry,
                            $"obstacle '{relevant[i].StableId}' has invalid navigation bounds");
                if (!BuildNodes(start, goal, relevant, profile, nodes, result))
                    return Fail(result, ShuttleRouteFailure.TooManyRelevantObstacles,
                        "visibility graph exceeded its configured node budget");
                debugEdges.Clear();
                edges.Clear();
                for (int i = 0; i < nodes.Count; i++) edges.Add(new List<int>());
                discoveries.Clear();
                for (int i = 0; i < nodes.Count; i++)
                {
                    for (int j = i + 1; j < nodes.Count; j++)
                    {
                        if (edgeBudget <= 0) { budgetExceeded = true; break; }
                        edgeBudget--;
                        result.edgeSweepCount++;
                        SpaceNavigationObstacle blocker = SpaceClearanceQuery.FindFirstBlocker(
                            nodes[i], nodes[j], radius, ignoredRoot, layers);
                        if (blocker == null)
                        {
                            edges[i].Add(j);
                            edges[j].Add(i);
                            debugEdges.Add(new Vector2Int(i, j));
                        }
                        else if (!relevant.Contains(blocker) && !discoveries.Contains(blocker))
                            discoveries.Add(blocker);
                    }
                    if (budgetExceeded) break;
                }

                if (TryAStar(nodes, edges, out List<int> nodePath))
                {
                    for (int i = 0; i < nodePath.Count; i++) debugRawPath.Add(nodes[nodePath[i]]);
                    Simplify(debugRawPath, radius, ignoredRoot, layers, ref edgeBudget, result);
                    for (int i = 1; i < debugSimplifiedPath.Count - 1; i++)
                    {
                        Vector3 incoming = debugSimplifiedPath[i] - debugSimplifiedPath[i - 1];
                        Vector3 outgoing = debugSimplifiedPath[i + 1] - debugSimplifiedPath[i];
                        float angle = Vector3.Angle(incoming, outgoing);
                        float multiplier = angle < profile.straightTurnDegrees ? 1f :
                            angle < profile.moderateTurnDegrees ? profile.moderateSpeedMultiplier :
                            angle < profile.sharpTurnDegrees ? profile.sharpSpeedMultiplier : profile.hairpinSpeedMultiplier;
                        float passSpeed = Mathf.Max(0.1f, maxCruiseSpeed * multiplier);
                        FlightWaypoint waypoint = FlightWaypoint.Cruise(debugSimplifiedPath[i],
                            Mathf.Max(profile.navigationRadius, 1f), passSpeed);
                        result.cruiseWaypoints.Add(waypoint);
                        debugPassSpeeds.Add(passSpeed);
                    }
                    result.kind = ShuttleRouteKind.Visibility;
                    result.diagnostic = "bounded visibility route found";
                    result.relevantObstacleCount = relevant.Count;
                    result.candidateNodeCount = nodes.Count - 2;
                    return result;
                }

                bool added = false;
                for (int i = 0; i < discoveries.Count; i++)
                {
                    if (relevant.Count >= profile.maxRelevantObstacles)
                        return Fail(result, ShuttleRouteFailure.TooManyRelevantObstacles,
                            "route planning reached the relevant-obstacle budget");
                    relevant.Add(discoveries[i]);
                    result.relevantObstacleCount = relevant.Count;
                    added = true;
                }
                if (budgetExceeded)
                    return Fail(result, ShuttleRouteFailure.NoRouteWithinBudget,
                        "route planning reached the edge-sweep budget");
                if (!added)
                    return Fail(result, ShuttleRouteFailure.GraphDisconnected,
                        "visibility graph has no connected path within the configured rounds");
            }
            return Fail(result, ShuttleRouteFailure.NoRouteWithinBudget,
                "route planning reached the expansion-round budget");
        }

        private bool BuildNodes(Vector3 start, Vector3 goal, List<SpaceNavigationObstacle> obstacles,
            ShuttleNavigationProfile profile, List<Vector3> nodes, ShuttleRoutePlan result)
        {
            nodes.Clear(); debugCandidates.Clear(); nodes.Add(start); nodes.Add(goal);
            Vector3 axis = (goal - start).sqrMagnitude > 0.0001f ? (goal - start).normalized : Vector3.forward;
            Vector3 reference = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) < 0.9f ? Vector3.up : Vector3.right;
            Vector3 right = Vector3.Cross(axis, reference).normalized;
            Vector3 up = Vector3.Cross(right, axis).normalized;
            int ringPoints = Mathf.Min(8, profile.maxCandidatesPerObstacle / 2);
            for (int i = 0; i < obstacles.Count; i++)
            {
                SpaceNavigationObstacle obstacle = obstacles[i];
                float r = obstacle.AvoidanceRadius + profile.SweptRadius + profile.detourPadding;
                if (!ShuttleFlightIntegrator.IsFinite(r) || r <= 0f)
                    return false;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 ringCenter = obstacle.ConservativeCenter + axis * (side * r * 0.45f);
                    for (int n = 0; n < ringPoints; n++)
                    {
                        float angle = n * (Mathf.PI * 2f / ringPoints);
                        Vector3 point = ringCenter + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * r;
                        if (nodes.Count >= profile.maxGraphNodes)
                            return false;
                        nodes.Add(point);
                        debugCandidates.Add(point);
                    }
                }
            }
            result.candidateNodeCount = nodes.Count - 2;
            debugGraphNodes.Clear(); debugGraphNodes.AddRange(nodes);
            return true;
        }

        private static bool TryAStar(List<Vector3> nodes, List<List<int>> edges, out List<int> path)
        {
            int count = nodes.Count;
            float[] score = new float[count];
            float[] estimate = new float[count];
            int[] previous = new int[count];
            bool[] closed = new bool[count];
            for (int i = 0; i < count; i++) { score[i] = float.PositiveInfinity; estimate[i] = float.PositiveInfinity; previous[i] = -1; }
            score[0] = 0f; estimate[0] = Vector3.Distance(nodes[0], nodes[1]);
            for (int iteration = 0; iteration < count; iteration++)
            {
                int current = -1;
                for (int i = 0; i < count; i++)
                    if (!closed[i] && (current < 0 || estimate[i] < estimate[current] - 0.00001f ||
                        (Mathf.Abs(estimate[i] - estimate[current]) <= 0.00001f && i < current))) current = i;
                if (current < 0 || float.IsPositiveInfinity(estimate[current])) break;
                if (current == 1)
                {
                    path = new List<int>();
                    for (int at = current; at >= 0; at = previous[at]) { path.Add(at); if (at == 0) break; }
                    path.Reverse();
                    return path.Count > 1 && path[0] == 0 && path[path.Count - 1] == 1;
                }
                closed[current] = true;
                List<int> adjacent = edges[current];
                adjacent.Sort();
                for (int i = 0; i < adjacent.Count; i++)
                {
                    int next = adjacent[i];
                    if (closed[next]) continue;
                    float candidate = score[current] + Vector3.Distance(nodes[current], nodes[next]);
                    if (candidate < score[next] - 0.00001f)
                    {
                        previous[next] = current;
                        score[next] = candidate;
                        estimate[next] = candidate + Vector3.Distance(nodes[next], nodes[1]);
                    }
                }
            }
            path = null;
            return false;
        }

        private void Simplify(List<Vector3> raw, float radius, Transform ignoredRoot,
            int layers, ref int edgeBudget, ShuttleRoutePlan result)
        {
            debugSimplifiedPath.Clear();
            if (raw.Count < 2) return;
            int current = 0;
            debugSimplifiedPath.Add(raw[current]);
            while (current < raw.Count - 1)
            {
                int furthest = current + 1;
                for (int candidate = raw.Count - 1; candidate > current + 1; candidate--)
                {
                    if (edgeBudget <= 0) break;
                    edgeBudget--; result.edgeSweepCount++;
                    if (SpaceClearanceQuery.IsSweptSegmentClear(raw[current], raw[candidate], radius,
                            ignoredRoot, out _, layers)) { furthest = candidate; break; }
                }
                current = furthest;
                debugSimplifiedPath.Add(raw[current]);
            }
        }

        private static ShuttleRoutePlan Fail(ShuttleRoutePlan result, ShuttleRouteFailure failure, string message)
        {
            result.failure = failure; result.diagnostic = message; return result;
        }

        private void ClearDebug()
        {
            debugCandidates.Clear(); debugGraphNodes.Clear(); debugRawPath.Clear(); debugSimplifiedPath.Clear(); debugEdges.Clear(); debugPassSpeeds.Clear();
        }

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.2f);
            for (int i = 0; i < debugCandidates.Count; i++) Gizmos.DrawWireSphere(debugCandidates[i], 0.3f);
            Gizmos.color = new Color(0.4f, 0.4f, 0.4f, 0.25f);
            for (int i = 0; i < debugEdges.Count; i++)
            {
                Vector2Int edge = debugEdges[i];
                // Candidate indices follow Start and Goal in the graph; draw only when a raw path exists.
                if (edge.x < debugGraphNodes.Count && edge.y < debugGraphNodes.Count)
                {
                    Gizmos.DrawLine(debugGraphNodes[edge.x], debugGraphNodes[edge.y]);
                }
            }
            DrawPath(debugRawPath, Color.yellow);
            DrawPath(debugSimplifiedPath, Color.cyan);
#if UNITY_EDITOR
            for (int i = 0; i < debugPassSpeeds.Count && i + 1 < debugSimplifiedPath.Count - 1; i++)
                Handles.Label(debugSimplifiedPath[i + 1] + Vector3.up * 0.5f,
                    $"Pass ≤ {debugPassSpeeds[i]:0.0}");
#endif
        }

        private static void DrawPath(List<Vector3> path, Color color)
        {
            Gizmos.color = color;
            for (int i = 0; i + 1 < path.Count; i++) Gizmos.DrawLine(path[i], path[i + 1]);
        }
    }
}
