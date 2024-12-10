using System;

namespace IntelOrca.Biohazard.BioRand.Routing
{
    public readonly struct Requirement : IEquatable<Requirement>
    {
        private readonly object _value;

        public int Id => _value == null ? -1 : IsKey ? ((Key)_value).Id : ((Node)_value).Id;
        public bool IsKey => _value is Key;
        public bool IsNode => _value is Node;
        public bool IsUninitialized => _value == null;
        public Key? Key => IsKey ? (Key)_value : null;
        public Node? Node => IsNode ? (Node)_value : null;

        public Requirement(Key key)
        {
            _value = key;
        }

        public Requirement(Node node)
        {
            _value = node;
        }

        public bool Equals(Requirement other) => _value == other._value;
        public override bool Equals(object obj) => obj is Requirement other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public override string ToString() => _value.ToString();

        public static bool operator ==(Requirement lhs, Requirement rhs) => lhs.Equals(rhs);
        public static bool operator !=(Requirement lhs, Requirement rhs) => lhs != rhs;

        public static implicit operator Requirement(Key k) => new(k);
        public static implicit operator Requirement(Node n) => new(n);
    }
}
