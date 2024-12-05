using System;
using System.Threading.Tasks;

namespace IntelOrca.Biohazard.BioRand
{
    public interface IRandomizerAgentHandler
    {
        IRandomizer Randomizer { get; }
        Task<bool> CanGenerateAsync(RandomizerAgent.QueueResponseItem queueItem);
        Task<RandomizerOutput> GenerateAsync(RandomizerAgent.QueueResponseItem queueItem, RandomizerInput input);
        void LogInfo(string message);
        void LogError(Exception ex, string message);
    }
}
