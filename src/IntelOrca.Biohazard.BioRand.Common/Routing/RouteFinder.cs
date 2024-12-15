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
            var m = input.ToMermaid(true);
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

        private static State DoSubgraph(State state, Node start, bool first, Random rng)
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
                state = DoSubgraph(state, n, first: false, rng);
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
        /// <exception cref="NotImplementedException"></exception>
        private static int GetRemovableKeyCount(State state, Key key, Edge edge)
        {
            return RequirementFinderState.GetRemovableKeyCount(state, edge.Destination, key);
        }

        private static HashSet<Requirement> GetGuaranteedRequirements(State state, Node root)
        {
            return RequirementFinderState.FindReusableRequirements(state, root);
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

        private class RequirementFinderState
        {
            public State State { get; }
            public ImmutableHashSet<Node> Nodes { get; }
            public ImmutableList<Edge> Edges { get; }

            public Graph Input => State.Input;

            private RequirementFinderState(State state, ImmutableHashSet<Node> nodes, ImmutableList<Edge> edges)
            {
                State = state;
                Edges = edges;
                Nodes = nodes;
            }

            public static HashSet<Requirement> FindReusableRequirements(State state, Node end)
            {
                var result = new HashSet<Requirement>();
                var paths = FindPaths(state, end);
                if (paths.Length != 0)
                {
                    result.UnionWith(paths[0].Requirements);
                    foreach (var p in paths.Skip(1))
                    {
                        result.IntersectWith(p.Requirements);
                    }
                }
                return result;
            }

            public static int GetRemovableKeyCount(State state, Node end, Key find)
            {
                var result = (int?)null;
                var paths = FindPaths(state, end);
                foreach (var p in paths)
                {
                    var c = p.Requirements.Count(x => x.Key is Key k && k == find);
                    result = result is int r ? Math.Min(r, c) : c;
                }
                return result ?? 0;
            }

            public static RequirementFinderState[] FindPaths(State state, Node end)
            {
                var finderState = new RequirementFinderState(state, [end], []);
                var result = finderState.Continue(end).ToArray();
                return result;
            }

            private IEnumerable<RequirementFinderState> Continue(Node target)
            {
                var edges = Input.GetApplicableEdgesTo(target)
                    .Where(x => !Nodes.Contains(x.Source) || !Nodes.Contains(x.Destination))
                    .ToArray();
                return Continue(target, edges);
            }

            private IEnumerable<RequirementFinderState> Continue(Node target, IEnumerable<Edge> edges)
            {
                if (ReachedEnd)
                {
                    yield return this;
                    yield break;
                }

                foreach (var edge in edges)
                {
                    var other = edge.Source == target ? edge.Destination : edge.Source;
                    var choice = Fork(other, edge);
                    foreach (var c in choice.Continue(other))
                    {
                        yield return c;
                    }
                }
            }

            public RequirementFinderState Fork(Node node, Edge edge)
            {
                return new RequirementFinderState(State, Nodes.Add(node), Edges.Add(edge));
            }

            public bool ReachedEnd => Edges.Count == 0
                ? Nodes.Contains(Input.Start)
                : Edges.Last().Contains(Input.Start);

            public Requirement[] Requirements
            {
                get
                {
                    if (Edges.Count == 0)
                        return [];

                    var endNode = Edges[0].Destination;
                    return Edges
                        .SelectMany(x => x.Requires)
                        .Concat(Nodes.Where(x => x != endNode).Select(x => new Requirement(x)))
                        .ToArray();
                }
            }
        }
    }
}
