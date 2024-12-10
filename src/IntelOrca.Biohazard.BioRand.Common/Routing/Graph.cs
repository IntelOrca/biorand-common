using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace IntelOrca.Biohazard.BioRand.Routing
{
    public sealed class Graph
    {
        public ImmutableArray<Key> Keys { get; }
        public ImmutableArray<Node> Nodes { get; }
        public ImmutableArray<Edge> Edges { get; }

        public ImmutableDictionary<Node, ImmutableArray<Edge>> EdgeMap { get; }
        public ImmutableDictionary<Node, ImmutableArray<Edge>> InverseEdgeMap { get; }
        public ImmutableArray<Node> Start { get; }
        public ImmutableArray<ImmutableArray<Node>> Subgraphs { get; }

        public Graph(
            ImmutableArray<Key> keys,
            ImmutableArray<Node> nodes,
            ImmutableArray<Edge> edges)
        {
            Keys = keys;
            Nodes = nodes;
            Edges = edges;
            EdgeMap = edges
                .GroupBy(x => x.Source)
                .ToImmutableDictionary(x => x.Key, x => x.ToImmutableArray());
            InverseEdgeMap = edges
                .GroupBy(x => x.Destination)
                .ToImmutableDictionary(x => x.Key, x => x.ToImmutableArray());

            var targets = Edges.Select(x => x.Destination).ToImmutableHashSet();
            Start = nodes.Where(x => !targets.Contains(x)).ToImmutableArray();

            Subgraphs = GetSubgraphs();
        }

        public ImmutableArray<Edge> GetEdges(Node node)
        {
            return EdgeMap.TryGetValue(node, out var edges) ? edges : [];
        }

        public ImmutableArray<Edge> GetEdgesTo(Node node)
        {
            return InverseEdgeMap.TryGetValue(node, out var edges) ? edges : [];
        }

        private string[] GetKeys(Edge e)
        {
            return e.Requires
                .OfType<Key>()
                .Select(e => string.Join(" ", GetIcon(e), $"K<sub>{e.Id}</sub>"))
                .ToArray();
        }

        private static string GetIcon(Key k)
        {
            if (k.Kind == KeyKind.Consumable)
                return "fa:fa-triangle-exclamation";
            if (k.Kind == KeyKind.Removable)
                return "fa:fa-circle";
            return "";
        }

        private ImmutableArray<ImmutableArray<Node>> GetSubgraphs()
        {
            var graphs = new List<ImmutableArray<Node>>();
            var visited = new HashSet<Node>();
            var next = Start as IEnumerable<Node>;
            while (next.Any())
            {
                var (g, end) = GetEndNodes(next);
                graphs.Add(g.ToImmutableArray());
                next = end;
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
                    visited.Add(n);
                    nodes.Add(n);

                    var edges = GetEdges(n);
                    foreach (var e in edges)
                    {
                        if (e.OneWay)
                        {
                            end.Add(e.Source);
                        }
                        else
                        {
                            q.Enqueue(e.Destination);
                        }
                    }
                }
                return (nodes.ToArray(), end.Where(x => !visited.Contains(x)).ToArray());
            }
        }

        public string ToMermaid(bool useLabels = false)
        {
            var mb = new MermaidBuilder();
            mb.Node("S", " ", MermaidShape.Circle);
            for (int gIndex = 0; gIndex < Subgraphs.Length; gIndex++)
            {
                var g = Subgraphs[gIndex];
                mb.BeginSubgraph($"G<sub>{gIndex}</sub>");
                foreach (var node in g)
                {
                    var (letter, shape) = GetNodeLabel(node);
                    var label = $"{letter}<sub>{node.Id}</sub>";
                    if (useLabels && !string.IsNullOrEmpty(node.Label))
                        label = node.Label;
                    mb.Node(GetNodeName(node), label, shape);
                }
                mb.EndSubgraph();
            }

            foreach (var node in Start)
            {
                mb.Edge("S", GetNodeName(node));
            }

            foreach (var edge in Edges)
            {
                EmitEdge(edge);
            }
            return mb.ToString();

            void EmitEdge(Edge edge)
            {
                var sourceName = GetNodeName(edge.Source);
                var targetName = GetNodeName(edge.Destination);
                var label = string.Join(" + ", GetKeys(edge));
                var edgeType = edge.OneWay
                    ? MermaidEdgeType.Dotted
                    : MermaidEdgeType.Solid;
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
    }
}
