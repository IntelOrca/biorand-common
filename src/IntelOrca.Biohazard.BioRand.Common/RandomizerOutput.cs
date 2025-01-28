using System.Collections.Generic;
using System.Collections.Immutable;

namespace IntelOrca.Biohazard.BioRand
{
    public class RandomizerOutput(
        ImmutableArray<RandomizerOutputAsset> assets,
        string instructions,
        Dictionary<string, string> logs)
    {
        public ImmutableArray<RandomizerOutputAsset> Assets => assets;
        public string Instructions => instructions;
        public Dictionary<string, string> Logs => logs;
    }
}
