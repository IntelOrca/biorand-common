using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace IntelOrca.Biohazard.BioRand.Common.Tests
{
    public class TestAgent
    {
        [Fact]
        public async Task Randomize()
        {
            var host = "http://localhost:10285";
            var apiKey = "ENTER-API-KEY-HERE";
            var game = 1;
            using var agent = new RandomizerAgent(host, apiKey, game, new Handler());
            await agent.RunAsync();
        }

        private class Handler : IRandomizer, IRandomizerAgentHandler
        {
            public IRandomizer Randomizer => this;
            public RandomizerConfigurationDefinition ConfigurationDefinition => new RandomizerConfigurationDefinition();
            public RandomizerConfiguration DefaultConfiguration => new RandomizerConfiguration();
            public string BuildVersion => "1.0";
            public RandomizerOutput Randomize(RandomizerInput input)
            {
                return new RandomizerOutput(
                    new byte[16],
                    new byte[16],
                    new Dictionary<string, string>());
            }

            public Task<bool> CanGenerateAsync(RandomizerAgent.QueueResponseItem queueItem) => Task.FromResult(true);
            public Task<RandomizerOutput> GenerateAsync(RandomizerAgent.QueueResponseItem queueItem, RandomizerInput input) => Task.FromResult(Randomizer.Randomize(input));
            public void LogError(Exception ex, string message) => Assert.Fail(message);
            public void LogInfo(string message) { }
        }
    }
}
