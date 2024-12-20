using System;

namespace IntelOrca.Biohazard.BioRand.Routing
{
    public readonly struct Key(int id, int group, KeyKind kind, string? label) : IEquatable<Key>
    {
        public int Id => id;
        public int Group => group;
        public KeyKind Kind => kind;
        public string? Label => label;
        public bool IsDefault => id == 0;
        public override int GetHashCode() => id.GetHashCode();
        public override bool Equals(object obj) => obj is Key k && Equals(k);
        public bool Equals(Key other) => id == other.Id;
        public override string ToString() => $"#{Id} ({Label})" ?? $"#{Id}";
        public static bool operator ==(Key lhs, Key rhs) => lhs.Equals(rhs);
        public static bool operator !=(Key lhs, Key rhs) => lhs != rhs;
    }
}
