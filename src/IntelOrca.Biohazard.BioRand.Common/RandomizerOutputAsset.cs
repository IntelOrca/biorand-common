namespace IntelOrca.Biohazard.BioRand
{
    public sealed class RandomizerOutputAsset(
        string key,
        string title,
        string description,
        string fileName,
        byte[] data)
    {
        public string Key => key;
        public string Title => title;
        public string Description => description;
        public string FileName => fileName;
        public byte[] Data => data;
    }
}
