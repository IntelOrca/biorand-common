using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using IntelOrca.Biohazard.BioRand.Graphing;

namespace IntelOrca.Biohazard.BioRand.Routing
{
    public sealed class Graph
    {
        public ImmutableArray<Key> Keys { get; }
        public ImmutableArray<Node> Nodes { get; }
        public ImmutableArray<Edge> Edges { get; }

        public ImmutableDictionary<Node, ImmutableArray<Edge>> EdgeMap { get; }
        public ImmutableDictionary<Node, ImmutableArray<Edge>> InverseEdgeMap { get; }
        public Node Start { get; }
        public ImmutableArray<ImmutableArray<Node>> Subgraphs { get; }

        public Graph(
            ImmutableArray<Key> keys,
            ImmutableArray<Node> nodes,
            ImmutableArray<Edge> edges)
        {
            if (nodes.IsDefaultOrEmpty)
                throw new ArgumentException("Graph contains no nodes", nameof(nodes));

            Keys = keys;
            Nodes = nodes;
            Edges = edges;
            EdgeMap = edges
                .GroupBy(x => x.Source)
                .ToImmutableDictionary(x => x.Key, x => x.ToImmutableArray());
            InverseEdgeMap = edges
                .GroupBy(x => x.Destination)
                .ToImmutableDictionary(x => x.Key, x => x.ToImmutableArray());
            Start = nodes.First();
            Subgraphs = GetSubgraphs();
            ValidateNoReturns();
        }

        public ImmutableArray<Edge> GetEdges(Node node)
        {
            return GetEdgesFrom(node).AddRange(GetEdgesTo(node));
        }

        public ImmutableArray<Edge> GetEdgesFrom(Node node)
        {
            return EdgeMap.TryGetValue(node, out var edges) ? edges : [];
        }

        public ImmutableArray<Edge> GetEdgesTo(Node node)
        {
            return InverseEdgeMap.TryGetValue(node, out var edges) ? edges : [];
        }

        public ImmutableArray<Edge> GetApplicableEdgesFrom(Node node)
        {
            var from = GetEdgesFrom(node);
            var to = GetEdgesTo(node).Where(x => x.Kind == EdgeKind.TwoWay);
            return from.Concat(to).ToImmutableArray();
        }

        public ImmutableArray<Edge> GetApplicableEdgesTo(Node node)
        {
            var from = GetEdgesFrom(node).Where(x => x.Kind == EdgeKind.TwoWay);
            var to = GetEdgesTo(node);
            return from.Concat(to).ToImmutableArray();
        }

        private string[] GetKeys(Edge e, bool useLabels)
        {
            return e.Requires
                .Select(r => string.Join(" ", GetIcon(r), GetLabel(r)))
                .ToArray();

            string GetLabel(Requirement r)
            {
                var result = $"K<sub>{r.Id}</sub>";
                return useLabels ? r.Label ?? result : result;
            }
        }

        private static string GetIcon(Requirement r)
        {
            if (r.Key is Key k1 && k1.Kind == KeyKind.Consumable)
                return "fa:fa-triangle-exclamation";
            if (r.Key is Key k2 && k2.Kind == KeyKind.Removable)
                return "fa:fa-circle";
            if (r.IsNode)
                return "fa:fa-circle";
            return "";
        }

        private ImmutableArray<ImmutableArray<Node>> GetSubgraphs()
        {
            var graphs = new List<ImmutableArray<Node>>();
            var visited = new HashSet<Node>();
            var unvisited = new HashSet<Node>(Nodes);
            while (unvisited.Count != 0)
            {
                var next = new[] { unvisited.First() };
                while (next.Any())
                {
                    var (g, end) = GetEndNodes(next);
                    graphs.Add([.. g]);
                    next = end;
                }
            }
            return graphs.ToImmutableArray();

            (Node[], Node[]) GetEndNodes(IEnumerable<Node> start)
            {
                var nodes = new List<Node>();
                var end = new List<Node>();
                var q = new Queue<Node>(start);
                while (q.Count != 0)
                {
                    var n = q.Dequeue();
                    if (!visited.Add(n))
                        continue;

                    unvisited.Remove(n);
                    nodes.Add(n);

                    var edges = GetApplicableEdgesFrom(n);
                    foreach (var e in edges)
                    {
                        if (e.Kind == EdgeKind.OneWay || e.Kind == EdgeKind.NoReturn)
                        {
                            end.Add(e.Destination);
                        }
                        else
                        {
                            q.Enqueue(e.Source);
                            q.Enqueue(e.Destination);
                        }
                    }
                }
                return ([.. nodes], end.Where(x => !visited.Contains(x)).ToArray());
            }
        }

        private void ValidateNoReturns()
        {
            Search(Start, []);

            void Search(Node input, HashSet<Node> bad)
            {
                var q = new Queue<Node>([input]);
                var exit = new HashSet<Node>();
                var visited = new HashSet<Node>();
                while (q.Count != 0)
                {
                    var n = q.Dequeue();
                    if (!visited.Add(n))
                        continue;
                    if (!bad.Add(n))
                        throw new GraphException("No return edge returns back.");

                    var edges = GetEdges(n);
                    foreach (var e in edges)
                    {
                        if (e.Kind == EdgeKind.NoReturn)
                        {
                            if (e.Source == n)
                            {
                                exit.Add(e.Destination);
                            }
                        }
                        else
                        {
                            q.Enqueue(e.Inverse(n));
                        }
                    }
                }
                foreach (var e in exit)
                {
                    Search(e, [.. bad]);
                }
            }
        }

        public string ToMermaid(bool useLabels = false, bool includeItems = true)
        {
            var mb = new MermaidBuilder();
            mb.Node("S", " ", MermaidShape.Circle);
            for (int gIndex = 0; gIndex < Subgraphs.Length; gIndex++)
            {
                var g = Subgraphs[gIndex];
                mb.BeginSubgraph($"G<sub>{gIndex}</sub>");
                foreach (var node in g)
                {
                    if (!includeItems && node.IsItem)
                        continue;

                    var (letter, shape) = GetNodeLabel(node);
                    var label = $"{letter}<sub>{node.Id}</sub>";
                    if (useLabels && !string.IsNullOrEmpty(node.Label))
                        label = node.Label;
                    mb.Node(GetNodeName(node), label, shape);
                }
                mb.EndSubgraph();
            }

            mb.Edge("S", GetNodeName(Start));
            foreach (var edge in Edges)
            {
                if (!includeItems && edge.Destination.IsItem)
                    continue;

                EmitEdge(edge);
            }
            return mb.ToString();

            void EmitEdge(Edge edge)
            {
                var sourceName = GetNodeName(edge.Source);
                var targetName = GetNodeName(edge.Destination);
                var label = string.Join(" + ", GetKeys(edge, useLabels));
                var edgeType = edge.Kind == EdgeKind.TwoWay
                    ? MermaidEdgeType.Solid
                    : MermaidEdgeType.Dotted;
                mb.Edge(sourceName, targetName, label, edgeType);
            }

            static string GetNodeName(Node node)
            {
                return $"N{node.Id}";
            }

            static (char, MermaidShape) GetNodeLabel(Node node)
            {
                return node.Kind switch
                {
                    NodeKind.Item => ('I', MermaidShape.Square),
                    _ => ('R', MermaidShape.Circle),
                };
            }
        }

        public Route GenerateRoute(int? seed = null)
        {
            return new RouteFinder(seed).Find(this);
        }
    }
}
