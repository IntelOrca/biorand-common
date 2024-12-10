using System.Collections.Generic;

namespace IntelOrca.Biohazard.BioRand.Routing
{
    public class GraphBuilder
    {
        private readonly List<Key> _keys = [];
        private readonly List<Node> _nodes = [];
        private readonly List<Edge> _edges = [];
        private int _id;

        private int GetNextId()
        {
            return ++_id;
        }

        public Key ReusuableKey(int group, string? label)
        {
            var key = new Key(GetNextId(), group, KeyKind.Reusuable, label);
            _keys.Add(key);
            return key;
        }

        public Key ConsumableKey(int group, string? label)
        {
            var key = new Key(GetNextId(), group, KeyKind.Consumable, label);
            _keys.Add(key);
            return key;
        }

        public Key RemovableKey(int group, string? label)
        {
            var key = new Key(GetNextId(), group, KeyKind.Removable, label);
            _keys.Add(key);
            return key;
        }

        public Node Item(int group, string? label, Node source, params IRequirement[] requires)
        {
            var node = new Node(GetNextId(), group, NodeKind.Item, label);
            _nodes.Add(node);
            _edges.Add(new Edge(source, node, [.. requires], false));
            return node;
        }

        public Node AndGate(string? label)
        {
            var node = new Node(GetNextId(), 0, NodeKind.Default, label);
            _nodes.Add(node);
            return node;
        }

        public Node AndGate(string? label, Node source, params IRequirement[] requires)
        {
            var node = new Node(GetNextId(), 0, NodeKind.Default, label);
            _nodes.Add(node);
            _edges.Add(new Edge(source, node, [.. requires], false));
            return node;
        }

        public Node OrGate(string? label, params Node[] sources)
        {
            var node = new Node(GetNextId(), 0, NodeKind.Default, label);
            _nodes.Add(node);
            foreach (var r in sources)
            {
                _edges.Add(new Edge(r, node, [], false));
                _edges.Add(new Edge(r, node, [], false));
            }
            return node;
        }

        public Node OneWay(string? label, Node source, params IRequirement[] requires)
        {
            var node = new Node(GetNextId(), 0, NodeKind.Default, label);
            _nodes.Add(node);
            _edges.Add(new Edge(source, node, [.. requires], true));
            return node;
        }

        public Graph Build()
        {
            return new Graph([.. _keys], [.. _nodes], [.. _edges]);
        }

        public Route GenerateRoute(int? seed = null)
        {
            return new RouteFinder(seed).Find(Build());
        }
    }
}
