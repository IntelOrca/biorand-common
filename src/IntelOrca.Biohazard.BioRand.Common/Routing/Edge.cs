using System;
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

    public interface IRequirement
    {
        public int Id { get; }
    }

    public readonly struct Key(int id, int group, KeyKind kind, string? label) : IEquatable<Key>, IRequirement
    {
        public int Id => id;
        public int Group => group;
        public KeyKind Kind => kind;
        public string? Label => label;
        public override int GetHashCode() => id.GetHashCode();
        public override bool Equals(object obj) => obj is Key k && Equals(k);
        public bool Equals(Key other) => id == other.Id;
        public override string ToString() => $"#{Id} ({Label})" ?? $"#{Id}";
        public static bool operator ==(Key lhs, Key rhs) => lhs.Equals(rhs);
        public static bool operator !=(Key lhs, Key rhs) => lhs != rhs;
    }

    public enum KeyKind
    {
        Reusuable,
        Consumable,
        Removable
    }
}
