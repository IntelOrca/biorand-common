using System.Collections.Immutable;

namespace IntelOrca.Biohazard.BioRand.Collections
{
    public sealed class ImmutableOneToManyDictionary<TOne, TMany>
        where TOne : notnull
        where TMany : notnull
    {
        public static ImmutableOneToManyDictionary<TOne, TMany> Empty { get; } = new ImmutableOneToManyDictionary<TOne, TMany>();

        private readonly ImmutableDictionary<TOne, TMany> _keyToValue;
        private readonly ImmutableDictionary<TMany, ImmutableHashSet<TOne>> _valueToKeys;

        private ImmutableOneToManyDictionary()
        {
            _keyToValue = ImmutableDictionary<TOne, TMany>.Empty;
            _valueToKeys = ImmutableDictionary<TMany, ImmutableHashSet<TOne>>.Empty;
        }

        internal ImmutableOneToManyDictionary(
            ImmutableDictionary<TOne, TMany> keyToValue,
            ImmutableDictionary<TMany, ImmutableHashSet<TOne>> valueToKeys)
        {
            _keyToValue = keyToValue;
            _valueToKeys = valueToKeys;
        }

        public int Count => _keyToValue.Count;

        public TMany this[TOne key] => _keyToValue[key];

        public ImmutableHashSet<TOne> GetKeysContainingValue(TMany value)
        {
            if (_valueToKeys.TryGetValue(value, out var keys))
                return keys;
            return ImmutableHashSet<TOne>.Empty;
        }

        public bool TryGetValue(
            TOne key,
#if NET
            [MaybeNullWhen(false)]
#endif
            out TMany value)
        {
#pragma warning disable CS8601 // Possible null reference assignment.
            return _keyToValue.TryGetValue(key, out value);
#pragma warning restore CS8601 // Possible null reference assignment.
        }

        public ImmutableOneToManyDictionary<TOne, TMany> Add(TOne key, TMany value)
        {
            var newKeyToValue = _keyToValue.Add(key, value);
            var newValueToKeys = _valueToKeys;
            if (_valueToKeys.TryGetValue(value, out var set))
            {
                newValueToKeys = newValueToKeys.SetItem(value, set.Add(key));
            }
            else
            {
                newValueToKeys = _valueToKeys.Add(value, ImmutableHashSet.Create(key));
            }
            return new ImmutableOneToManyDictionary<TOne, TMany>(newKeyToValue, newValueToKeys);
        }
    }
}
