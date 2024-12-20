using System;

namespace IntelOrca.Biohazard.BioRand.Routing
{
    public readonly struct Requirement : IEquatable<Requirement>
    {
        private readonly object _value;

        public int Id => _value == null ? -1 : IsKey ? ((Key)_value).Id : ((Node)_value).Id;
        public string? Label => _value == null ? null : IsKey ? ((Key)_value).Label : ((Node)_value).Label;
        public bool IsKey => _value is Key;
        public bool IsNode => _value is Node;
        public bool IsUninitialized => _value == null;
        public Key? Key => IsKey ? (Key)_value : null;
        public Node? Node => IsNode ? (Node)_value : null;

        public Requirement(Key key)
        {
            if (key.IsDefault)
                throw new ArgumentException("Key is uninitialized", nameof(key));
            _value = key;
        }

        public Requirement(Node node)
        {
            if (node.IsDefault)
                throw new ArgumentException("Node is uninitialized", nameof(node));
            _value = node;
        }

        public bool Equals(Requirement other) => _value.Equals(other._value);
        public override bool Equals(object obj) => obj is Requirement other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public override string ToString() => _value.ToString();

        public static bool operator ==(Requirement lhs, Requirement rhs) => lhs.Equals(rhs);
        public static bool operator !=(Requirement lhs, Requirement rhs) => lhs != rhs;

        public static implicit operator Requirement(Key k) => new(k);
        public static implicit operator Requirement(Node n) => new(n);
    }
}
