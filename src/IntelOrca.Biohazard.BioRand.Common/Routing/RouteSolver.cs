using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using IntelOrca.Biohazard.BioRand.Collections;

namespace IntelOrca.Biohazard.BioRand.Routing
{
    public class RouteSolver
    {
        public static RouteSolver Default => new RouteSolver();

        private RouteSolver()
        {
        }

        public RouteSolverResult Solve(Route route)
        {
            var state = Begin(route);
            while (true)
            {
                state = Expand(state);
                var newState = UseKey(state);
                if (newState == null)
                    return RouteSolverResult.PotentialSoftlock | RouteSolverResult.NodesRemaining;
                if (newState == state)
                    break;
                state = newState;
            }

            RouteSolverResult flags = 0;
            if (state.Next.Count != 0)
                flags |= RouteSolverResult.NodesRemaining;
            return flags;
        }

        private static State Begin(Route route)
        {
            return new State(
                route,
                ImmutableHashSet.CreateRange(route.Graph.Start),
                ImmutableHashSet.CreateRange(route.Graph.Start.SelectMany(x => route.Graph.GetEdges(x))),
                ImmutableMultiSet<Key>.Empty);
        }

        private static State Expand(State state)
        {
            var graph = state.Route.Graph;
            var newVisits = new List<Edge>();
            do
            {
                newVisits.Clear();
                foreach (var edge in state.Next)
                {
                    if (!edge.Requires.OfType<Node>().All(state.Visited.Contains))
                        continue;

                    newVisits.Add(edge);
                }
                state = state.Visit(newVisits);
            } while (newVisits.Count != 0);
            return state;
        }

        private static State? UseKey(State state)
        {
            var graph = state.Route.Graph;
            var possibleWays = state.Next
                .Where(x => HasAllKeys(state, x))
                .ToArray();

            // Lets first unlock anything that doesn't consume a key
            var safeWays = possibleWays
                .Where(x => x.Requires.OfType<Key>().All(x => x.Kind != KeyKind.Consumable))
                .ToArray();
            if (safeWays.Length != 0)
            {
                foreach (var way in safeWays)
                {
                    state = state.Visit(way);
                }
                return state;
            }

            // Only possible ways left consume a key, so lets detect we can
            // do all of them
            foreach (var way in possibleWays)
            {
                if (!HasAllKeys(state, way))
                    return null;

                var consumeKeys = way.Requires
                    .OfType<Key>()
                    .Where(x => x.Kind == KeyKind.Consumable)
                    .ToArray();
                state = state.UseKeys(consumeKeys);
            }

            // Now visit everything we unlocked
            foreach (var way in possibleWays)
            {
                state = state.Visit(way);
            }

            return state;
        }

        private static bool HasAllKeys(State state, Edge edge)
        {
            var keys = state.Keys;
            var requiredKeys = edge.Requires
                .OfType<Key>()
                .ToArray();
            foreach (var g in requiredKeys.GroupBy(x => x))
            {
                var have = keys.GetCount(g.Key);
                var need = g.Count();
                if (have < need)
                {
                    return false;
                }
            }
            return true;
        }

        private class State
        {
            public Route Route { get; }
            public ImmutableHashSet<Node> Visited { get; }
            public ImmutableHashSet<Edge> Next { get; }
            public ImmutableMultiSet<Key> Keys { get; }

            public State(
                Route route,
                ImmutableHashSet<Node> visited,
                ImmutableHashSet<Edge> next,
                ImmutableMultiSet<Key> keys)
            {
                Route = route;
                Visited = visited;
                Next = next;
                Keys = keys;
            }

            public State Visit(params Edge[] edges) => Visit((IEnumerable<Edge>)edges);
            public State Visit(IEnumerable<Edge> edges)
            {
                if (!edges.Any())
                    return this;

                var newNodes = edges
                    .Select(x => x.Destination)
                    .Where(x => !Visited.Contains(x))
                    .ToArray();
                var newEdges = newNodes.SelectMany(x => Route.Graph.GetEdges(x));
                var newKeys = newNodes
                    .Select(Route.GetItemContents)
                    .Where(x => x != null)
                    .Select(x => x!.Value)
                    .ToArray();
                return new State(
                    Route,
                    Visited.Union(newNodes),
                    Next.Except(edges).Union(newEdges),
                    Keys.AddRange(newKeys));
            }

            public State AddKey(Key key)
            {
                return new State(Route, Visited, Next, Keys.Add(key));
            }

            public State UseKeys(IEnumerable<Key> keys)
            {
                if (!keys.Any())
                    return this;

                return new State(Route, Visited, Next, Keys.RemoveMany(keys));
            }
        }
    }
}
