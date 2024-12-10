using System.Collections.Generic;
using System.Collections.Immutable;

namespace IntelOrca.Biohazard.BioRand.Collections
{
    public class OneToManyDictionary<TOne, TMany>
        where TOne : notnull
        where TMany : notnull
    {
        private Dictionary<TOne, TMany> _keyToValue = new Dictionary<TOne, TMany>();
        private Dictionary<TMany, HashSet<TOne>> _valueToKeys = new Dictionary<TMany, HashSet<TOne>>();

        public TMany this[TOne key] => _keyToValue[key];

        public bool TryGetValue(TOne key, out TMany value) => _keyToValue.TryGetValue(key, out value);

        public void Add(TOne key, TMany value)
        {
            _keyToValue.Add(key, value);
            var set = GetManyToOneList(value);
            set.Add(key);
        }

        public ISet<TOne> GetKeysContainingValue(TMany value)
        {
            if (_valueToKeys.TryGetValue(value, out var keys))
                return keys;
            return ImmutableHashSet<TOne>.Empty;
        }

        private HashSet<TOne> GetManyToOneList(TMany value)
        {
            if (!_valueToKeys.TryGetValue(value, out var set))
            {
                set = new HashSet<TOne>();
                _valueToKeys.Add(value, set);
            }
            return set;
        }

        public ImmutableOneToManyDictionary<TOne, TMany> ToImmutable()
        {
            return new ImmutableOneToManyDictionary<TOne, TMany>(
                _keyToValue.ToImmutableDictionary(),
                _valueToKeys.ToImmutableDictionary(x => x.Key, x => x.Value.ToImmutableHashSet()));
        }
    }
}
