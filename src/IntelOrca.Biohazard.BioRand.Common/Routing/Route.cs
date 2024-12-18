﻿using System.Collections.Immutable;
using IntelOrca.Biohazard.BioRand.Collections;

namespace IntelOrca.Biohazard.BioRand.Routing
{
    public sealed class Route
    {
        public Graph Graph { get; }
        public bool AllNodesVisited { get; }
        public ImmutableOneToManyDictionary<Node, Key> ItemToKey { get; }
        public string Log { get; }

        public Route(
            Graph graph,
            bool allNodesVisited,
            ImmutableOneToManyDictionary<Node, Key> itemToKey,
            string log)
        {
            Graph = graph;
            AllNodesVisited = allNodesVisited;
            ItemToKey = itemToKey;
            Log = log;
        }

        public Key? GetItemContents(Node item)
        {
            if (ItemToKey.TryGetValue(item, out var key))
                return key;
            return null;
        }

        public ImmutableHashSet<Node> GetItemsContainingKey(Key key)
        {
            return ItemToKey.GetKeysContainingValue(key);
        }

#if false
        public string GetDependencyTree(Node node, bool keysAsNodes = false)
        {
            var visited = new HashSet<Node>();
            var mb = new MermaidBuilder();
            Visit(node);
            return mb.ToString();

            void Visit(Node n)
            {
                if (!visited.Add(n))
                    return;

                if (keysAsNodes || n.IsKey)
                {
                    var label = n.Label;
                    if (n.Kind == NodeKind.Item && !keysAsNodes)
                    {
                        if (ItemToKey.TryGetValue(n, out var key))
                        {
                            label += $"\n<small>{key}</small>";
                        }
                    }
                    mb.Node($"N{n.Id}", label,
                        n.Kind switch
                        {
                            NodeKind.ReusuableKey => MermaidShape.Hexagon,
                            NodeKind.ConsumableKey => MermaidShape.Hexagon,
                            NodeKind.RemovableKey => MermaidShape.Hexagon,
                            NodeKind.Item => keysAsNodes
                                ? MermaidShape.Square
                                : MermaidShape.DoubleSquare,
                            _ => MermaidShape.Circle,
                        });
                }
                if (n.IsKey)
                {
                    var items = ItemToKey.GetKeysContainingValue(n);
                    foreach (var item in items)
                    {
                        Visit(item);
                        if (keysAsNodes)
                            mb.Edge($"N{item.Id}", $"N{n.Id}",
                                type: items.Count == 1 ? MermaidEdgeType.Solid : MermaidEdgeType.Dotted);
                    }
                }
                else
                {
                    foreach (var r in n.Requires)
                    {
                        Visit(r);
                        if (r.IsKey && !keysAsNodes)
                        {
                            var items = ItemToKey.GetKeysContainingValue(n);
                            foreach (var item in items)
                            {
                                Visit(item);
                                mb.Edge($"N{item.Id}", $"N{n.Id}",
                                    type: items.Count == 1 ? MermaidEdgeType.Solid : MermaidEdgeType.Dotted);
                            }
                        }
                        else
                        {
                            mb.Edge($"N{r.Id}", $"N{n.Id}");
                        }
                    }
                }
            }
        }
#endif

        public RouteSolverResult Solve() => RouteSolver.Default.Solve(this);
    }
}
