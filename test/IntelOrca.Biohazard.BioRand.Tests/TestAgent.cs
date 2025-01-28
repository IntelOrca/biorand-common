using System;
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
                    [
                        new RandomizerOutputAsset(
                            "1-patch",
                            "Patch",
                            "Simply drop this file into your RE 4 install folder.",
                            "biorand-re4r-58252-mod.zip",
                            new byte[16]),
                        new RandomizerOutputAsset(
                            "2-fluffy",
                            "Fluffy Mod",
                            "Drop this zip file into Fluffy Mod Manager's mod folder and enable it.",
                            "biorand-re4r-58252.zip",
                            new byte[16])
                    ],
                    """
                    <p class="mt-3">What should I do if my game crashes?</p>
                    <ol class="ml-8 list-decimal text-gray-300">
                      <li>Reload from last checkpoint and try again.</li>
                      <li>Alter the enemy sliders slightly or reduce the number temporarily. This will reshuffle the enemies. Reload from last checkpoint and try again.</li> <li>As a last resort, change your seed, and reload from last checkpoint.</li>
                    </ol>
                    """,
                    []);
            }

            public Task<bool> CanGenerateAsync(RandomizerAgent.QueueResponseItem queueItem) => Task.FromResult(true);
            public Task<RandomizerOutput> GenerateAsync(RandomizerAgent.QueueResponseItem queueItem, RandomizerInput input) => Task.FromResult(Randomizer.Randomize(input));
            public void LogError(Exception ex, string message) => Assert.Fail(message);
            public void LogInfo(string message) { }
        }
    }
}
