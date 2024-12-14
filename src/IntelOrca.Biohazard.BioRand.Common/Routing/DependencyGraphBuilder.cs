namespace IntelOrca.Biohazard.BioRand.Routing
{
    /// <summary>
    /// Builds a graph by passing in the requirements (dependencies)
    /// when creating each node.
    /// </summary>
    public class DependencyGraphBuilder
    {
        private readonly GraphBuilder _graphBuilder = new();
        private int _id;

        private int GetNextId()
        {
            return ++_id;
        }

        public Key ReusuableKey(int group, string? label)
        {
            return _graphBuilder.Key(label, group);
        }

        public Key ConsumableKey(int group, string? label)
        {
            return _graphBuilder.Key(label, group, KeyKind.Consumable);
        }

        public Key RemovableKey(int group, string? label)
        {
            return _graphBuilder.Key(label, group, KeyKind.Removable);
        }

        public Node Item(int group, string? label, Node source, params Requirement[] requires)
        {
            return _graphBuilder.Item(label, group, source, requires);
        }

        public Node AndGate(string? label)
        {
            return _graphBuilder.Room(label);
        }

        public Node AndGate(string? label, Node source, params Requirement[] requires)
        {
            var node = _graphBuilder.Room(label);
            _graphBuilder.Door(source, node, requires);
            return node;
        }

        public Node OrGate(string? label, params Node[] sources)
        {
            var node = _graphBuilder.Room(label);
            foreach (var r in sources)
            {
                _graphBuilder.Door(r, node);
            }
            return node;
        }

        public Node OneWay(string? label, Node source, params Requirement[] requires)
        {
            var node = _graphBuilder.Room(label);
            _graphBuilder.OneWay(source, node, requires);
            return node;
        }

        public Node NoReturn(string? label, Node source, params Requirement[] requires)
        {
            var node = _graphBuilder.Room(label);
            _graphBuilder.NoReturn(source, node, requires);
            return node;
        }

        public Graph Build()
        {
            return _graphBuilder.ToGraph();
        }

        public Route GenerateRoute(int? seed = null)
        {
            return new RouteFinder(seed).Find(Build());
        }
    }
}
