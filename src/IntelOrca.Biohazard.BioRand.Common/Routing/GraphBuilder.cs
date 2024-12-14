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
            var item = new Node(GetNextId(), group, NodeKind.Item, label);
            _nodes.Add(item);
            Edge(room, item, EdgeKind.TwoWay, requires);
            return item;
        }

        public Edge Edge(Node sourceRoom, Node targetRoom, EdgeKind kind, params Requirement[] requires)
        {
            var edge = new Edge(sourceRoom, targetRoom, [.. requires], kind);
            _edges.Add(edge);
            return edge;
        }

        public Edge Door(Node sourceRoom, Node targetRoom, params Requirement[] requires)
        {
            return Edge(sourceRoom, targetRoom, EdgeKind.TwoWay, requires);
        }

        public Edge BlockedDoor(Node sourceRoom, Node targetRoom, params Requirement[] requires)
        {
            return Edge(sourceRoom, targetRoom, EdgeKind.UnlockTwoWay, requires);
        }

        public Edge OneWay(Node sourceRoom, Node targetRoom, params Requirement[] requires)
        {
            return Edge(sourceRoom, targetRoom, EdgeKind.OneWay, requires);
        }

        public Edge NoReturn(Node sourceRoom, Node targetRoom, params Requirement[] requires)
        {
            return Edge(sourceRoom, targetRoom, EdgeKind.NoReturn, requires);
        }

        public Graph ToGraph()
        {
            return new Graph([.. _keys], [.. _nodes], [.. _edges]);
        }

        public Route GenerateRoute(int? seed = null)
        {
            return new RouteFinder(seed).Find(ToGraph());
        }
    }
}
