using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace IntelOrca.Biohazard.BioRand.Common
{
    public class RandomizerClient(string baseUri)
    {
        private const string DefaultBaseUri = "https://api.biorand.net";

        private readonly HttpClient _httpClient = new();
        private readonly JsonSerializerOptions _options = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        private string _authToken = "";

        public string BaseUri => baseUri;

        public RandomizerClient() : this(DefaultBaseUri) { }

        public string AuthToken
        {
            get => _authToken;
            set
            {
                value ??= "";
                if (_authToken != value)
                {
                    _authToken = value;
                    _httpClient.DefaultRequestHeaders.Authorization = value == ""
                        ? null :
                        new AuthenticationHeaderValue("Bearer", _authToken);
                }
            }
        }

        private string GetUri(string path)
        {
            return $"{BaseUri}/{path}";
        }

        private async Task<T> SendAsync<T>(HttpMethod method, string path, object? data = null) where T : class
        {
            var url = GetUri(path);
            var request = new HttpRequestMessage(method, url);
            if (data != null)
            {
                var json = JsonSerializer.Serialize(data, _options);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"{response.StatusCode} returned");

            var responseContent = await response.Content.ReadAsStringAsync();
            if (responseContent.Length == 0)
                return null!;

            return JsonSerializer.Deserialize<T>(responseContent, _options)!;
        }

        private Task<T> GetAsync<T>(string path) where T : class => SendAsync<T>(HttpMethod.Get, path)!;

        public Task<Game[]> GetGamesAsync() => GetAsync<Game[]>("game");

        public class Game
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string Moniker { get; set; } = "";
        }
    }
}
