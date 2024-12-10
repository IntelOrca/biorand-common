using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

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
            state = DoSubgraph(state, input.Start, first: true, _rng);
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

        private static State DoSubgraph(State state, IEnumerable<Node> start, bool first, Random rng)
        {
            var keys = new List<Key>();
            var visited = new List<Node>();
            var next = new List<Edge>();
            var toVisit = new List<Node>();
            foreach (var n in start)
            {
                // var deps = GetHardDependencies(state, n);
                // keys.AddRange(deps.Where(x => x.IsKey));
                // visited.AddRange(deps.Where(x => !x.IsKey));
                // if (first)
                //     next.Add(n);
                // else
                toVisit.Add(n);
            }

            state = state.AddLog($"Begin subgraph {start.First()}");
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
                var required = GetRequiredKeys2(state, n);

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
                    if (state.Visited.Contains(e.Destination))
                        continue;

                    if (e.OneWay)
                    {
                        newState = newState.AddOneWay(e.Destination);
                    }
                    else
                    {
                        newState = newState.VisitNode(e.Destination);
                    }
                }
                state = newState;
            }
            return state;
        }

        private static List<Key> GetRequiredKeys2(State state, Edge edge)
        {
            var required = GetMissingKeys(state, state.Keys, edge);
            var newKeys = state.Keys.AddRange(required);
            foreach (var n in state.Next)
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
            var requiredKeys = edge.Requires
                .OfType<Key>()
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
                    if (available[j].Group == keys[i].Group)
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
            var subGraphs = state.OneWay.ToArray();
            foreach (var n in subGraphs)
            {
                state = DoSubgraph(state, new[] { n }, first: false, rng);
            }
            return state;
        }

        private static (State, Edge[]) TakeNextNodes(State state)
        {
            var result = new List<Edge>();
            while (true)
            {
                var next = state.Next.ToArray();
                var index = Array.FindIndex(next, x => IsSatisfied(state, x));
                if (index == -1)
                    break;

                var edge = next[index];
                result.Add(edge);

                // Remove any keys from inventory if they are consumable
                var consumableKeys = edge.Requires
                    .OfType<Key>()
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
        /// <exception cref="NotImplementedException"></exception>
        private static int GetRemovableKeyCount(State state, Key key, Edge edge)
        {
            var selfCount = 0;
            foreach (var r in edge.Requires)
            {
                if (r.Equals(key))
                {
                    selfCount++;
                }
            }
            var edges = state.Input.GetEdgesTo(edge.Source);
            int? minCount = null;
            foreach (var e in edges)
            {
                var c = GetRemovableKeyCount(state, key, e);
                minCount = minCount is int mc ? Math.Min(mc, c) : c;
            }
            return selfCount + (minCount ?? 0);
        }

#if false
        private static HashSet<Node> GetHardDependencies(State state, Node node)
        {
            var set = new HashSet<Node>();
            Recurse(node);
            return set;

            void Recurse(Node node)
            {
                foreach (var r in node.Requires)
                {
                    if (r.IsKey)
                    {
                        var items = state.ItemToKey.GetKeysContainingValue(r);
                        if (items.Any())
                        {
                            var item = items.FirstOrDefault();
                            set.Add(r);
                            set.Add(item);
                            Recurse(item);
                        }
                    }
                    else
                    {
                        set.Add(r);
                        Recurse(r);
                    }
                }
            }
        }
#endif

        private static ChecklistItem GetChecklistItem(State state, Edge edge)
        {
            var haveList = new List<Key>();
            var missingList = new List<Key>();
            var requiredKeys = GetRequiredKeys(state, edge)
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

            return edge.Requires
                .OfType<Node>()
                .All(state.Visited.Contains);
        }

        private static Key[] GetRequiredKeys(State state, Edge edge)
        {
            var leaves = new List<Key>();
            GetRequiredKeys(edge);
            return [.. leaves];

            void GetRequiredKeys(Edge e)
            {
                if (state.Visited.Contains(e.Destination))
                    return;

                foreach (var r in e.Requires)
                {
                    if (r is Key k)
                    {
                        leaves.Add(k);
                    }
                }
            }
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
                result.Next = Next.Union(Input.GetEdges(node));
                result.Log = Log.Add($"Satisfied node: {node}");
                return result;
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
