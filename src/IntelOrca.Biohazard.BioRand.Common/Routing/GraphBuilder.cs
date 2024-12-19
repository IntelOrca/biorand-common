using System;
using System.Collections.Generic;
using System.Linq;

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
            if (sourceRoom.IsDefault)
                throw new ArgumentException("Source node is uninitialized", nameof(sourceRoom));
            if (targetRoom.IsDefault)
                throw new ArgumentException("Target node is uninitialized", nameof(targetRoom));

            var index = _edges.FindIndex(x => x.Source == sourceRoom && x.Destination == targetRoom);
            if (index != -1)
            {
                var existingEdge = _edges[index];
                if (existingEdge.Kind != kind)
                    throw new Exception("Duplicate edge added with different kind");
                if (!existingEdge.Requires.SequenceEqual(requires))
                    throw new Exception("Duplicate edge added with different requirements");

                return existingEdge;
            }
            index = _edges.FindIndex(x => x.Source == targetRoom && x.Destination == sourceRoom);
            if (index != -1)
            {
                var existingEdge = _edges[index];
                if (existingEdge.Kind == EdgeKind.TwoWay && kind == EdgeKind.TwoWay)
                {
                    if (existingEdge.Requires.SequenceEqual(requires))
                    {
                        return existingEdge;
                    }
                    else
                    {
                        if (existingEdge.Requires.Length != 0)
                        {
                            if (requires.Length != 0)
                            {
                                throw new Exception("Duplicate inverse edge added with different requirements");
                            }
                            return existingEdge;
                        }
                        // Allow replacement of 0 requirements
                    }
                }
                else if (existingEdge.Kind == EdgeKind.NoReturn || kind == EdgeKind.NoReturn)
                {
                    throw new Exception("Duplicate inverse edge added, one of which is set to NoReturn");
                }
                else if (existingEdge.Kind == EdgeKind.OneWay || kind == EdgeKind.OneWay)
                {
                    throw new Exception("Duplicate inverse edge added, one of which is set to OneWay");
                }
                else if (existingEdge.Kind == EdgeKind.TwoWay || kind == EdgeKind.UnlockTwoWay)
                {
                    if (existingEdge.Requires.Length != 0)
                        throw new Exception("Duplicate inverse edge found with requirements when adding unblock");
                }
                else if (existingEdge.Kind == EdgeKind.UnlockTwoWay || kind == EdgeKind.TwoWay)
                {
                    if (requires.Length != 0)
                        throw new Exception("Duplicate inverse unblock edge found when this side has requirements");
                    return _edges[index];
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            var edge = new Edge(sourceRoom, targetRoom, [.. requires], kind);
            if (index != -1)
                _edges[index] = edge;
            else
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
