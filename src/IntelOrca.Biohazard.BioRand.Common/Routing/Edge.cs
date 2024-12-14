using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace IntelOrca.Biohazard.BioRand.Routing
{
    public readonly struct Edge(Node source, Node destination, ImmutableArray<Requirement> requires, EdgeKind kind)
    {
        public Node Source => source;
        public Node Destination => destination;
        public ImmutableArray<Requirement> Requires => requires;
        public EdgeKind Kind => kind;

        public IEnumerable<Key> RequiredKeys => requires.Where(x => x.IsKey).Select(x => x.Key!.Value);
        public IEnumerable<Node> RequiredNodes => requires.Where(x => x.IsNode).Select(x => x.Node!.Value);

        public override string ToString()
        {
            var requires = Requires.Length == 0 ? "" : $" [{string.Join(", ", Requires)}] ";
            return kind switch
            {
                EdgeKind.TwoWay => $"{Source} <={requires}=> {Destination}",
                EdgeKind.UnlockTwoWay => $"{Source} =={requires}=> {Destination}",
                EdgeKind.OneWay => $"{Source} --{requires}-> {Destination}",
                EdgeKind.NoReturn => $"{Source} | {requires} |> {Destination}",
                _ => throw new NotSupportedException(),
            };
        }
    }

    public enum EdgeKind
    {
        /// <summary>
        /// Player can go either way through the edge. If the edge has requirements,
        /// the player can satisfy them from either direction.
        /// Example would be a door which takes a key, and that key can be used from
        /// either side. Once unlocked the door remains open in both directions.
        /// </summary>
        TwoWay,

        /// <summary>
        /// Player can go either way through the edge once the requirements have been
        /// satisfied. They can only be initially satisfied from one direction though.
        /// If there are no requirements, then the door is treated as <see cref="OneWay"/>.
        /// Example would be a door with chains, the bolt cutter must be used from
        /// one side, but afterwards, the door is two way.
        /// </summary>
        UnlockTwoWay,

        /// <summary>
        /// Player can only ever go one way through the door, it can only be unlocked
        /// from that direction. However it is possible for the player to get back to
        /// the original side of the door again.
        /// Example would be a ladder that breaks in RE2R, or the clock tower chute in RE2.
        /// </summary>
        OneWay,

        /// <summary>
        /// Player can go through the door once, and never return. For example the
        /// lift / cable car to the lab in RE2/R.
        /// </summary>
        NoReturn,
    }
}
