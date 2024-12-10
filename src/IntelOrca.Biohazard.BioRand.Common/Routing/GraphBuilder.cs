using System.Collections.Generic;

namespace IntelOrca.Biohazard.BioRand.Routing
{
    /// <summary>
    /// Helper for building a new graph to randomize the location
    /// of key items.
    /// </summary>
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

        public Key Key(KeyKind kind = KeyKind.Reusuable) => Key(kind: kind);
        public Key Key(string? label = null, int group = 0, KeyKind kind = KeyKind.Reusuable)
        {
            var key = new Key(GetNextId(), group, kind, label);
            _keys.Add(key);
            return key;
        }

        public Node Room(string? label = null)
        {
            var node = new Node(GetNextId(), 0, NodeKind.Default, label);
            _nodes.Add(node);
            return node;
        }

        public Node Item(Node room) => Item(null, 0, room);
        public Node Item(string? label, Node room, params Requirement[] requires) => Item(label, 0, room, requires);
        public Node Item(string? label, int group, Node room, params Requirement[] requires)
        {
            var item = new Node(GetNextId(), 0, NodeKind.Item, label);
            _nodes.Add(item);
            _edges.Add(new Edge(room, item, [.. requires], false));
            return item;
        }

        public Edge Door(Node sourceRoom, Node targetRoom, params Requirement[] requires)
        {
            var edge = new Edge(sourceRoom, targetRoom, [.. requires], false);
            _edges.Add(edge);
            return edge;
        }

        public Edge OneWay(Node sourceRoom, Node targetRoom, params Requirement[] requires)
        {
            var edge = new Edge(sourceRoom, targetRoom, [.. requires], true);
            _edges.Add(edge);
            return edge;
        }

        public Graph ToGraph()
        {
            return new Graph([.. _keys], [.. _nodes], [.. _edges]);
        }
    }
}
