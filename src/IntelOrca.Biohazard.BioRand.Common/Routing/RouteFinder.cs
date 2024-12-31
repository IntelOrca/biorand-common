using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using IntelOrca.Biohazard.BioRand.Collections;

namespace IntelOrca.Biohazard.BioRand.Routing
{
    public class RouteFinder
    {
        private readonly Random _rng = new Random();
        private readonly RouteFinderOptions _options = new RouteFinderOptions();

        public RouteFinder(int? seed = null, RouteFinderOptions? options = null)
        {
            if (seed != null)
                _rng = new Random(seed.Value);
            if (options != null)
                _options = options;
        }

        public Route Find(Graph input, CancellationToken ct = default)
        {
            var state = new State(input);
            state = DoSubgraph(_options, state, input.Start, _rng, false, 0, ct);
            return GetRoute(state);
        }

        private static Route GetRoute(State state)
        {
            return new Route(
                state.Input,
                state.Next.Count == 0,
                state.ItemToKey,
                string.Join("\n", state.Log));
        }

        private static State DoSubgraph(RouteFinderOptions options, State state, Node start, Random rng, bool fork, int depth, CancellationToken ct)
        {
            var guaranteedRequirements = GetGuaranteedRequirements(state, start);
            var keys = guaranteedRequirements.Where(x => x.IsKey).Select(x => x.Key!.Value).ToList();
            var visited = guaranteedRequirements.Where(x => !x.IsKey).Select(x => x.Node!.Value).ToList();
            var next = new List<Edge>();
            var toVisit = new List<Node> { start };

            if (fork)
                state = state.AddLog($"Begin fork {start}");
            else
                state = state.AddLog($"Begin subgraph {start}");
            if (fork)
                state = state.Fork(visited, keys, next);
            else
                state = state.Clear(visited, keys, next);
            foreach (var v in toVisit)
                state = state.VisitNode(v);

            return Fulfill(options, state, rng, depth, ct);
        }

        private static State Fulfill(RouteFinderOptions options, State state, Random rng, int depth, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (depth >= options.DebugDepthLimit)
            {
                throw new RouteFinderException("Depth limit reached", state);
            }

            state = Expand(state);
            if (!ValidateState(state))
                return state;

            // Choose to go down one way paths first
            state = FollowOneWayExits(options, state, rng, depth, ct);

            // Choose a door to open
            var bestState = state;
            var edgeToRequiredKeys = SortEdgesWithRequirements(rng, state);
            foreach (var required in edgeToRequiredKeys)
            {
                // TODO do something better here
                for (int retries = 0; retries < 10; retries++)
                {
                    var slots = FindAvailableSlots(rng, state, required);
                    if (slots == null)
                        continue;

                    var newState = state;
                    for (var i = 0; i < required.Count; i++)
                    {
                        newState = newState.PlaceKey(slots[i], required[i]);
                    }

                    var finalState = Fulfill(options, newState, rng, depth + 1, ct);
                    if (finalState.Next.Count == 0 && finalState.OneWay.Count == 0)
                    {
                        return finalState;
                    }
                    else if (finalState.ItemToKey.Count > bestState.ItemToKey.Count)
                    {
                        bestState = finalState;
                    }
                }
            }

            // If we have left over locked edges, don't bother continuing to next sub graph
            if (state.Next.Count != 0)
            {
                options.DebugDeadendCallback?.Invoke(state);
                return state;
            }

            return FollowNoReturnExits(options, bestState, rng, depth, ct);
        }

        private static List<Key>[] SortEdgesWithRequirements(Random rng, State state)
        {
            var next = Shuffle(rng, state.Next
                .OrderBy(x => x)
                .Select(x => GetRequiredKeys(state, x))
                .Where(x => x.Count != 0));
            Array.Sort(next, Compare);
            return next;

            int Compare(List<Key> a, List<Key> b)
            {
                var an = CountPlacedKeys(a);
                var bn = CountPlacedKeys(b);
                return an - bn;
            }

            int CountPlacedKeys(List<Key> keys)
            {
                var result = 0;
                foreach (var k in keys)
                {
                    if (k.Kind == KeyKind.Reusuable && state.ItemToKey.GetKeysContainingValue(k).Count != 0)
                    {
                        result++;
                    }
                }
                return result;
            }
        }

        private static State Expand(State state)
        {
            while (true)
            {
                var (newState, satisfied) = TakeNextNodes(state);
                if (satisfied.Length == 0)
                    break;

                foreach (var e in satisfied)
                {
                    if (newState.Visited.Contains(e.Source))
                    {
                        if (newState.Visited.Contains(e.Destination))
                            continue;

                        if (e.Kind == EdgeKind.OneWay || e.Kind == EdgeKind.NoReturn)
                        {
                            newState = newState.AddOneWay(e);
                        }
                        else
                        {
                            newState = newState.VisitNode(e.Destination);
                        }
                    }
                    else
                    {
                        newState = newState.VisitNode(e.Source);
                    }
                }
                state = newState;
            }
            return state;
        }

        private static List<Key> GetRequiredKeys(State state, Edge edge)
        {
            var required = GetMissingKeys(state, state.Keys, edge);
            var newKeys = state.Keys.AddRange(required);
            foreach (var n in state.Next.OrderBy(x => x))
            {
                if (n.Equals(edge))
                    continue;

                var missingKeys = GetMissingKeys(state, newKeys, n);
                if (missingKeys.Count == 0)
                {
                    missingKeys = GetMissingKeys(state, state.Keys, n);
                    foreach (var k in missingKeys)
                    {
                        if (k.Kind == KeyKind.Consumable)
                        {
                            required.Add(k);
                        }
                    }
                }
            }

            return [.. required];
        }

        private static List<Key> GetMissingKeys(State state, ImmutableMultiSet<Key> keys, Edge edge)
        {
            var requiredKeys = edge.RequiredKeys
                .GroupBy(x => x)
                .ToArray();

            var required = new List<Key>();
            foreach (var g in requiredKeys)
            {
                var have = keys.GetCount(g.Key);
                var need = g.Key.Kind == KeyKind.Removable
                    ? GetRemovableKeyCount(state, g.Key, edge)
                    : g.Count();
                need -= have;
                for (var i = 0; i < need; i++)
                {
                    required.Add(g.Key);
                }
            }

            return required;
        }

        private static Node[]? FindAvailableSlots(Random rng, State state, List<Key> keys)
        {
            if (state.SpareItems.Count < keys.Count)
                return null;

            var available = Shuffle(rng, state.SpareItems.OrderBy(x => x)).ToList();
            var result = new Node[keys.Count];
            for (var i = 0; i < keys.Count; i++)
            {
                var found = false;
                for (var j = 0; j < available.Count; j++)
                {
                    var itemGroup = available[j].Group;
                    var keyGroup = keys[i].Group;
                    if ((itemGroup & keyGroup) == keyGroup)
                    {
                        result[i] = available[j];
                        available.RemoveAt(j);
                        found = true;
                        break;
                    }
                }
                if (!found)
                    return null;
            }
            return result;
        }

        private static State FollowOneWayExits(RouteFinderOptions options, State state, Random rng, int depth, CancellationToken ct)
        {
            var subGraphs = Shuffle(rng, state.OneWay
                .Where(x => x.Kind == EdgeKind.OneWay)
                .OrderBy(x => x));
            if (subGraphs.Length > 0)
            {
                var e = subGraphs[0];
                state = state.RemoveOneWay(e);
                state = DoSubgraph(options, state, e.Destination, rng, true, depth, ct);
            }
            return state;
        }

        private static State FollowNoReturnExits(RouteFinderOptions options, State state, Random rng, int depth, CancellationToken ct)
        {
            var subGraphs = Shuffle(rng, state.OneWay
                .Where(x => x.Kind == EdgeKind.NoReturn)
                .OrderBy(x => x));
            foreach (var e in subGraphs)
            {
                state = state.RemoveOneWay(e);
                state = DoSubgraph(options, state, e.Destination, rng, false, depth, ct);
            }
            return state;
        }

        private static (State, Edge[]) TakeNextNodes(State state)
        {
            var result = new List<Edge>();
            while (true)
            {
                var next = state.Next.OrderBy(x => x).ToArray();
                var index = Array.FindIndex(next, x => IsSatisfied(state, x));
                if (index == -1)
                    break;

                var edge = next[index];
                result.Add(edge);

                // Remove any keys from inventory if they are consumable
                var consumableKeys = edge.RequiredKeys
                    .Where(x => x.Kind == KeyKind.Consumable)
                    .ToArray();
                state = state.UseKey(edge, consumableKeys);
            }
            return (state, result.ToArray());
        }

        /// <summary>
        /// Key the minimum number of occurances this given removal key requires
        /// to access the target node.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="key"></param>
        /// <param name="edge"></param>
        /// <returns></returns>
        private static int GetRemovableKeyCount(State state, Key key, Edge edge)
        {
            return Internal(edge.Destination, [], 0);

            int Internal(Node input, HashSet<Node> visited, int count)
            {
                if (!visited.Add(input))
                    return -1;

                if (input == state.Input.Start)
                    return count;

                var edges = state.Input.GetApplicableEdgesTo(input);
                var min = (int?)null;
                foreach (var e in edges)
                {
                    var other = e.Inverse(input);
                    var newCount = count + e.RequiredKeys.Count(x => x == key);
                    var r = Internal(other, visited, newCount);
                    if (r != -1)
                    {
                        min = min is int m ? Math.Min(m, r) : r;
                    }
                }
                return min ?? -1;
            }
        }

        private static HashSet<Requirement> GetGuaranteedRequirements(State state, Node root)
        {
            var map = new Dictionary<Node, HashSet<Requirement>>();
            map[state.Input.Start] = [new Requirement(state.Input.Start, isSoft: true)];
            foreach (var n in state.Input.Nodes)
            {
                map[n] = GetNodeRequirements(n, []) ?? [n];
            }

            var keyMap = new Dictionary<Key, HashSet<Requirement>>();
            foreach (var key in state.Input.Keys)
            {
                keyMap[key] = GetKeyRequirements(key, []);
            }

            var finalMap = new Dictionary<Node, HashSet<Requirement>>();
            var result = Final(root, []) ?? [];
            result.UnionWith(result
                .Where(x => x.Node is Node n && n.IsItem)
                .Select(x => new Requirement(state.ItemToKey[x.Node!.Value]))
                .ToArray());
            result.RemoveWhere(r => r.IsNode && r.IsSoft);
            result.RemoveWhere(r => r.Key is Key k && k.Kind != KeyKind.Reusuable);
            return result;

            HashSet<Requirement>? GetNodeRequirements(Node input, HashSet<Node> visited)
            {
                if (visited.Contains(input))
                    return null;

                if (map.TryGetValue(input, out var result))
                    return result;

                visited.Add(input);
                var sourceNodes = state.Input.GetApplicableEdgesTo(input);
                foreach (var e in sourceNodes)
                {
                    var other = e.Inverse(input);
                    var sub = GetNodeRequirements(other, visited);
                    if (sub != null)
                    {
                        if (result == null)
                            result = [.. sub, .. e.Requires];
                        else
                            result.IntersectWith([.. sub, .. e.Requires]);
                    }
                }
                result?.Add(new Requirement(input, isSoft: true));
                return result;
            }

            HashSet<Requirement> GetKeyRequirements(Key key, HashSet<Key> visited)
            {
                if (keyMap.TryGetValue(key, out var result))
                    return result;

                if (!visited.Add(key))
                    return [];

                var items = state.ItemToKey.GetKeysContainingValue(key);
                foreach (var item in items)
                {
                    var itemRequirements = new HashSet<Requirement>();
                    foreach (var ir in map[item])
                    {
                        if (ir.Key is Key k)
                        {
                            foreach (var subr in GetKeyRequirements(k, visited))
                            {
                                itemRequirements.Add(subr);
                            }
                        }
                        else
                        {
                            itemRequirements.Add(ir);
                        }
                    }
                    if (result == null)
                        result = itemRequirements;
                    else
                        result.IntersectWith(itemRequirements);
                }
                return result ?? [];
            }

            HashSet<Requirement>? Final(Node input, HashSet<Node> visited)
            {
                if (finalMap.TryGetValue(input, out var result))
                    return result;

                if (!visited.Add(input))
                    return null;

                result = [];
                foreach (var r in map[input])
                {
                    if (r.Key is Key k)
                    {
                        result.UnionWith(keyMap[k]);
                    }
                    else if (r.Node is Node n)
                    {
                        var sub = Final(n, visited);
                        if (sub != null)
                        {
                            result.UnionWith(sub);
                        }
                        if (!r.IsSoft)
                        {
                            result.Add(r);
                        }
                    }
                }
                return result;
            }
        }

        private static ChecklistItem GetChecklistItem(State state, Edge edge)
        {
            var haveList = new List<Key>();
            var missingList = new List<Key>();
            var requiredKeys = edge.RequiredKeys
                .GroupBy(x => x)
                .ToArray();

            foreach (var edges in requiredKeys)
            {
                var key = edges.Key;
                var need = edges.Count();
                var have = state.Keys.GetCount(key);

                if (key.Kind == KeyKind.Removable)
                {
                    need = GetRemovableKeyCount(state, key, edge);
                }

                var missing = Math.Max(0, need - have);
                for (var i = 0; i < missing; i++)
                    missingList.Add(key);

                var progress = Math.Min(have, need);
                for (var i = 0; i < progress; i++)
                    haveList.Add(key);
            }

            return new ChecklistItem(edge, [.. haveList], [.. missingList]);
        }

        private static bool ValidateState(State state)
        {
            var flags = RouteSolver.Default.Solve(GetRoute(state));
            return (flags & RouteSolverResult.PotentialSoftlock) == 0;
        }

        private sealed class ChecklistItem
        {
            public Edge Edge { get; }
            public ImmutableArray<Key> Have { get; }
            public ImmutableArray<Key> Need { get; }

            public ChecklistItem(Edge edge, ImmutableArray<Key> have, ImmutableArray<Key> need)
            {
                Edge = edge;
                Have = have;
                Need = need;
            }

            public override string ToString() => string.Format("{0} Have = {{{1}}} Need = {{{2}}}",
                Edge, string.Join(", ", Have), string.Join(", ", Need));
        }

        private static T[] Shuffle<T>(Random rng, IEnumerable<T> items)
        {
            var result = items.ToArray();
            for (var i = 0; i < result.Length; i++)
            {
                var j = rng.Next(0, i + 1);
                var tmp = result[i];
                result[i] = result[j];
                result[j] = tmp;
            }
            return result;
        }

        private static bool IsSatisfied(State state, Edge edge)
        {
            var checklistItem = GetChecklistItem(state, edge);
            if (checklistItem.Need.Length > 0)
                return false;

            return edge.RequiredNodes.All(state.Visited.Contains);
        }

        private sealed class State
        {
            public Graph Input { get; }
            public State? Parent { get; private set; }
            public ImmutableHashSet<Edge> Next { get; private set; } = [];
            public ImmutableHashSet<Edge> OneWay { get; private set; } = [];
            public ImmutableHashSet<Node> SpareItems { get; private set; } = [];
            public ImmutableHashSet<Node> Visited { get; private set; } = [];
            public ImmutableMultiSet<Key> Keys { get; private set; } = ImmutableMultiSet<Key>.Empty;
            public ImmutableOneToManyDictionary<Node, Key> ItemToKey { get; private set; } = ImmutableOneToManyDictionary<Node, Key>.Empty;
            public ImmutableList<string> Log { get; private set; } = [];

            public State(Graph input)
            {
                Input = input;
            }

            private State(State state)
            {
                Parent = state.Parent;
                Input = state.Input;
                Next = state.Next;
                OneWay = state.OneWay;
                SpareItems = state.SpareItems;
                Visited = state.Visited;
                Keys = state.Keys;
                ItemToKey = state.ItemToKey;
                Log = state.Log;
            }

            public State Clear(IEnumerable<Node> visited, IEnumerable<Key> keys, IEnumerable<Edge> next)
            {
                var result = new State(this)
                {
                    Visited = ImmutableHashSet<Node>.Empty.Union(visited),
                    Keys = ImmutableMultiSet<Key>.Empty.AddRange(keys),
                    Next = ImmutableHashSet<Edge>.Empty.Union(next),
                    OneWay = [],
                    SpareItems = []
                };
                return result;
            }

            public State Fork(IEnumerable<Node> visited, IEnumerable<Key> keys, IEnumerable<Edge> next)
            {
                var result = new State(this)
                {
                    Parent = this,
                    Visited = ImmutableHashSet<Node>.Empty.Union(visited),
                    Keys = ImmutableMultiSet<Key>.Empty.AddRange(keys),
                    Next = ImmutableHashSet<Edge>.Empty.Union(next),
                    OneWay = [],
                    SpareItems = []
                };
                return result;
            }

            public State RemoveOneWay(Edge edge)
            {
                var result = new State(this);
                result.OneWay = OneWay.Remove(edge);
                return result;
            }

            public State AddOneWay(Edge edge)
            {
                var result = new State(this);
                result.OneWay = OneWay.Add(edge);
                return result;
            }

            private State Join(State joinParent)
            {
                var result = new State(this);
                var curr = this;
                var depth = Depth;
                do
                {
                    curr = curr.Parent;
                    if (curr == null)
                        throw new ArgumentException("Parent to join with not found", nameof(joinParent));

                    result.Visited = result.Visited.Union(curr.Visited);
                    result.Keys = result.Keys.AddRange(curr.Keys);
                    result.Next = result.Next.Union(curr.Next);
                    result.OneWay = result.OneWay.Union(curr.OneWay);
                    result.SpareItems = result.SpareItems.Union(curr.SpareItems);
                    result.Log = result.Log.Add(TransformLogMessage(depth, "Join back to parent"));

                    foreach (var si in result.SpareItems)
                    {
                        if (result.ItemToKey.TryGetValue(si, out var k))
                        {
                            throw new Exception();
                        }
                    }

                    depth--;
                } while (curr != joinParent);
                result.Parent = joinParent.Parent;
                return result;
            }

            public State VisitNode(Node node)
            {
                // Can we join
                {
                    var parent = Parent;
                    while (parent != null)
                    {
                        if (parent.Visited.Contains(node))
                        {
                            return Join(parent);
                        }
                        parent = parent.Parent;
                    }
                }

                var result = new State(this);
                result.Visited = Visited.Add(node);
                if (node.Kind == NodeKind.Item)
                {
                    if (ItemToKey.TryGetValue(node, out var key))
                    {
                        result.Keys = Keys.Add(key);
                    }
                    else
                    {
                        result.SpareItems = SpareItems.Add(node);
                    }
                }

                var edges = Input.GetApplicableEdgesFrom(node)
                    .Where(x => !result.IsEdgeVisited(x))
                    .ToArray();

                result.Next = Next.Union(edges);
                result.Log = Log.Add(TransformLogMessage($"Visit node: {node}"));
                return result;
            }

            private bool IsEdgeVisited(Edge edge)
            {
                return Visited.Contains(edge.Source) && Visited.Contains(edge.Destination);
            }

            public State PlaceKey(Node item, Key key)
            {
                var result = new State(this);
                result.SpareItems = SpareItems.Remove(item);
                result.ItemToKey = ItemToKey.Add(item, key);
                result.Keys = Keys.Add(key);
                result.Log = Log.Add(TransformLogMessage($"Place {key} at {item}"));
                return result;
            }

            public State UseKey(Edge unlock, params Key[] keys)
            {
                var result = new State(this);
                result.Next = Next.Remove(unlock);
                result.Keys = Keys.RemoveMany(keys);
                return result;
            }

            public State AddLog(string message)
            {
                var state = new State(this);
                state.Log = Log.Add(TransformLogMessage(message));
                return state;
            }

            private string TransformLogMessage(string message) => TransformLogMessage(Depth, message);

            private string TransformLogMessage(int depth, string message)
            {
                return new string(' ', depth * 2) + message;
            }

            public int Depth
            {
                get
                {
                    var depth = 0;
                    var p = Parent;
                    while (p != null)
                    {
                        depth++;
                        p = p.Parent;
                    }
                    return depth;
                }
            }

            public string ToMermaid(bool useLabels = false, bool includeItems = true)
            {
                var visited = new HashSet<Node>();
                var s = this;
                while (s != null)
                {
                    visited.UnionWith(s.Visited);
                    s = s.Parent;
                }
                return Input.ToMermaid(useLabels, includeItems, Visited);
            }
        }
    }
}
