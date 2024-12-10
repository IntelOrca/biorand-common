using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace IntelOrca.Biohazard.BioRand.Routing
{
    public readonly struct Edge(Node source, Node destination, ImmutableArray<Requirement> requires, bool oneWay)
    {
        public Node Source => source;
        public Node Destination => destination;
        public ImmutableArray<Requirement> Requires => requires;
        public bool OneWay => oneWay;

        public IEnumerable<Key> RequiredKeys => requires.Where(x => x.IsKey).Select(x => x.Key!.Value);
        public IEnumerable<Node> RequiredNodes => requires.Where(x => x.IsNode).Select(x => x.Node!.Value);

        public override string ToString()
        {
            var requires = Requires.Length == 0 ? "" : $" [{string.Join(", ", Requires)}] ";
            return oneWay
                ? $"{Source} =={requires}=> {Destination}"
                : $"{Source} <={requires}=> {Destination}";
        }
    }
}
