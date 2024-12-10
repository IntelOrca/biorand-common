using System.Collections.Immutable;

namespace IntelOrca.Biohazard.BioRand.Routing
{
    public readonly struct Edge(Node source, Node destination, ImmutableArray<IRequirement> requires, bool oneWay)
    {
        public Node Source => source;
        public Node Destination => destination;
        public ImmutableArray<IRequirement> Requires => requires;
        public bool OneWay => oneWay;

        public override string ToString()
        {
            var requires = Requires.Length == 0 ? "" : $" [{string.Join(", ", Requires)}] ";
            return oneWay
                ? $"{Source} =={requires}=> {Destination}"
                : $"{Source} <={requires}=> {Destination}";
        }
    }
}
