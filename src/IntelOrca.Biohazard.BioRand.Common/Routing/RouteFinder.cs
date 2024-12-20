using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using IntelOrca.Biohazard.BioRand.Collections;

namespace IntelOrca.Biohazard.BioRand.Routing
{
    public class RouteFinder
    {
        private readonly Random _rng = new Random();

        public RouteFinder(int? seed = null)
        {
            if (seed != null)
                _rng = new Random(seed.Value);
        }

        public Route Find(Graph input)
        {
            var state = new State(input);
            state = DoSubgraph(state, input.Start, _rng);
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

        private static State DoSubgraph(State state, Node start, Random rng)
        {
            var guaranteedRequirements = GetGuaranteedRequirements(state, start);
            var keys = guaranteedRequirements.Where(x => x.IsKey).Select(x => x.Key!.Value).ToList();
            var visited = guaranteedRequirements.Where(x => !x.IsKey).Select(x => x.Node!.Value).ToList();
            var next = new List<Edge>();
            var toVisit = new List<Node> { start };

            state = state.AddLog($"Begin subgraph {start}");
            state = state.Clear(visited, keys, next);
            foreach (var v in toVisit)
                state = state.VisitNode(v);

            return Fulfill(state, rng);
        }

        private static State Fulfill(State state, Random rng)
        {
            state = Expand(state);
            if (!ValidateState(state))
                return state;

            // Choose a door to open
            var bestState = state;
            foreach (var n in Shuffle(rng, state.Next))
            {
                var required = GetRequiredKeys(state, n);

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

                    var finalState = Fulfill(newState, rng);
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
                return state;
            }

            return DoNextSubGraph(bestState, rng);
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
                    if (state.Visited.Contains(e.Source))
                    {
                        if (state.Visited.Contains(e.Destination))
                            continue;

                        if (e.Kind == EdgeKind.OneWay || e.Kind == EdgeKind.NoReturn)
                        {
                            newState = newState.AddOneWay(e.Destination);
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

            var available = Shuffle(rng, state.SpareItems).ToList();
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

        private static State DoNextSubGraph(State state, Random rng)
        {
            var subGraphs = Shuffle(rng, state.OneWay);
            foreach (var n in subGraphs)
            {
                state = DoSubgraph(state, n, rng);
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
            map[state.Input.Start] = [state.Input.Start];
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
                result?.Add(input);
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
            var result = items.OrderBy(x => x).ToArray();
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
            public ImmutableHashSet<Edge> Next { get; private set; } = [];
            public ImmutableHashSet<Node> OneWay { get; private set; } = [];
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

            public State AddOneWay(Node node)
            {
                var result = new State(this);
                result.OneWay = OneWay.Add(node);
                return result;
            }

            public State VisitNode(Node node)
            {
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
                result.Log = Log.Add($"Satisfied node: {node}");
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
                result.Log = Log.Add($"Place {key} at {item}");
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
                state.Log = Log.Add(message);
                return state;
            }
        }
    }
}
