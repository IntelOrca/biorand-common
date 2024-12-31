using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace IntelOrca.Biohazard.BioRand.Common.Tests
{
    public class TestRandomizerClient
    {
        [Fact]
        public async Task Games()
        {
            var client = CreateClient();
            var games = await client.GetGamesAsync();
            Assert.Equal([
                "1,re4r,Resident Evil 4 (2024)",
                "2,re2r,Resident Evil 2 (2019)"],
                games.Select(x => $"{x.Id},{x.Moniker},{x.Name}"));
        }

        private static RandomizerClient CreateClient()
        {
            var client = new RandomizerClient("https://beta-api.biorand.net");
            return client;
        }
    }
}
