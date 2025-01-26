using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using IntelOrca.Biohazard.BioRand.Common.Extensions;

namespace IntelOrca.Biohazard.BioRand
{
    public class RandomizerAgent : IDisposable
    {
        private const string StatusIdle = "Idle";
        private const string StatusGenerating = "Generating";
        private const string StatusUploading = "Uploading";

        private static readonly JsonSerializerOptions _options;

        private readonly HttpClient _httpClient = new();
        private readonly IRandomizerAgentHandler _handler;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);
        private DateTime _lastHeartbeatTime;
        private bool _shownWaitingMessage;
        private CancellationTokenSource _runCts = new();
        private Task _runTask = Task.CompletedTask;

        public IRandomizer Randomizer => _handler.Randomizer;
        public string BaseUri { get; }
        public string ApiKey { get; }
        public int GameId { get; }
        public Guid Id { get; private set; }
        public string Status { get; private set; } = StatusIdle;

        public TimeSpan HeartbeatTime { get; set; } = TimeSpan.FromSeconds(5);
        public TimeSpan PollTime { get; set; } = TimeSpan.FromSeconds(5);
        public TimeSpan RestartTime { get; set; } = TimeSpan.FromSeconds(5);

        public bool UseMultiPartFormUpload { get; } = true;

        static RandomizerAgent()
        {
            _options = new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            _options.Converters.Add(new UnixDateTimeConverter());
        }

        public RandomizerAgent(string baseUri, string apiKey, int gameId, IRandomizerAgentHandler handler)
        {
            BaseUri = baseUri;
            ApiKey = apiKey;
            GameId = gameId;
            _handler = handler;

            _httpClient.DefaultRequestHeaders.Add("X-API-KEY", ApiKey);
        }

        public void Dispose() => DisposeAsync().AsTask().Wait();

        public async ValueTask DisposeAsync()
        {
            if (_runTask != null && !_runTask.IsCompleted)
            {
                _runCts.Cancel();
                try
                {
                    await _runTask;
                }
                catch
                {
                }
            }
        }

        public async Task RunAsync(CancellationToken ct = default)
        {
            if (_runTask != null && !_runTask.IsCompleted)
            {
                throw new Exception("Already running");
            }

            _runCts = new CancellationTokenSource();
            _runTask = RunInternalAsync(CancellationTokenSource.CreateLinkedTokenSource(ct, _runCts.Token).Token);
            await _runTask;
        }

        private async Task RunInternalAsync(CancellationToken ct = default)
        {
            while (!ct.IsCancellationRequested)
            {
                if (await RegisterAsync())
                {
                    try
                    {
                        var localCts = new CancellationTokenSource();
                        var linked = CancellationTokenSource.CreateLinkedTokenSource(localCts.Token, ct);
                        var a = RunStatusLoopAsync(linked.Token);
                        var b = RunProcessLoopAsync(linked.Token);
                        try
                        {
                            await Task.WhenAny(a, b);
                        }
                        finally
                        {
                            localCts.Cancel();
                            await Task.WhenAll(a, b);
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        await UnregisterAsync();
                    }
                }
                await Task.Delay(RestartTime, ct);
            }
        }

        private async Task<bool> RegisterAsync()
        {
            _handler.LogInfo($"Registering agent at {BaseUri}...");
            try
            {
                var response = await PostAsync<RegisterResponse>("generator/register", new
                {
                    GameId,
                    Randomizer.ConfigurationDefinition,
                    Randomizer.DefaultConfiguration,
                });
                Id = response.Id;
                _handler.LogInfo($"Registered as agent {Id}");
                return true;
            }
            catch (Exception ex)
            {
                _handler.LogError(ex, "Failed to register agent");
                return false;
            }
        }

        private async Task UnregisterAsync()
        {
            _handler.LogInfo($"Unregistering agent...");
            try
            {
                await PostAsync<object>("generator/unregister", new { Id });
                _handler.LogInfo($"Unregistered agent {Id}");
            }
            catch (Exception ex)
            {
                _handler.LogError(ex, "Failed to unregister agent");
            }
        }

        private async Task RunStatusLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await _semaphore.WaitAsync();
                try
                {
                    var timeSinceLastHeartbeat = DateTime.UtcNow - _lastHeartbeatTime;
                    if (timeSinceLastHeartbeat >= HeartbeatTime)
                    {
                        await SendStatusAsync();
                    }
                }
                finally
                {
                    _semaphore.Release();
                }
                await Task.Delay(HeartbeatTime, ct);
            }
        }

        private async Task RunProcessLoopAsync(CancellationToken ct)
        {
            _shownWaitingMessage = false;
            while (!ct.IsCancellationRequested)
            {
                if (!await ProcessNextRandomizer(ct))
                {
                    await Task.Delay(PollTime, ct);
                }
            }
        }

        private async Task SendStatusAsync()
        {
            try
            {
                await PutAsync<object>("generator/heartbeat", new
                {
                    Id,
                    Status
                });
                _lastHeartbeatTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _handler.LogError(ex, "Failed to send heartbeat");
                throw;
            }
        }

        private async Task SetStatusAsync(string status)
        {
            await _semaphore.WaitAsync();
            try
            {
                Status = status;
                await SendStatusAsync();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task<bool> ProcessNextRandomizer(CancellationToken ct)
        {
            try
            {
                if (ct.IsCancellationRequested)
                    return false;

                if (!_shownWaitingMessage)
                {
                    _shownWaitingMessage = true;
                    _handler.LogInfo($"Waiting for next rando to generate...");
                }
                var queue = await GetAsync<QueueResponseItem[]>("generator/queue");
                foreach (var q in queue)
                {
                    if (ct.IsCancellationRequested)
                        return false;
                    if (q.GameId != GameId)
                        continue;

                    if (await _handler.CanGenerateAsync(q))
                    {
                        if (ct.IsCancellationRequested)
                            return false;

                        try
                        {
                            _handler.LogInfo($"Generating rando {q.Id}...");
                            _shownWaitingMessage = false;
                            await PostAsync<object>("generator/begin", new
                            {
                                Id,
                                RandoId = q.Id,
                                Version = Randomizer.BuildVersion
                            });
                        }
                        catch (Exception ex)
                        {
                            _handler.LogError(ex, "Failed to begin generating randomizer");
                            continue;
                        }
                        await GenerateRandomizer(q);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _handler.LogError(ex, "Failed to get randomizer queue");
            }
            return false;
        }

        private async Task GenerateRandomizer(QueueResponseItem q)
        {
            var randomizerInput = new RandomizerInput()
            {
                Configuration = RandomizerConfiguration.FromJson(q.Config!),
                ProfileName = q.ProfileName,
                ProfileAuthor = q.ProfileUserName,
                ProfileDescription = q.ProfileDescription,
                Seed = q.Seed
            };

            try
            {
                await SetStatusAsync(StatusGenerating);
                RandomizerOutput output;
                try
                {
                    output = await _handler.GenerateAsync(q, randomizerInput);
                }
                catch (Exception ex)
                {
                    var reason = ex is RandomizerUserException ue ? ue.Message : "An internal error occured generating the randomizer.";
                    await PostAsync<object>("generator/fail", new
                    {
                        Id,
                        RandoId = q.Id,
                        Reason = reason
                    });
                    _handler.LogError(ex, "Failed to generate randomizer");
                    return;
                }

                await SetStatusAsync(StatusUploading);
                _handler.LogInfo($"Uploading rando {q.Id}...");
                try
                {
                    if (UseMultiPartFormUpload)
                    {
                        await PostFormAsync<object>("generator/end-form", new Dictionary<string, object>
                        {
                            ["id"] = Id,
                            ["randoId"] = q.Id,
                            ["pakOutput"] = output.PakOutput,
                            ["fluffyOutput"] = output.FluffyOutput
                        });
                    }
                    else
                    {
                        await PostAsync<object>("generator/end", new
                        {
                            Id,
                            RandoId = q.Id,
                            output.PakOutput,
                            output.FluffyOutput
                        });
                    }
                    _handler.LogInfo($"Uploaded rando {q.Id}");
                }
                catch (Exception ex)
                {
                    _handler.LogError(ex, "Failed to upload rando");
                }
            }
            finally
            {
                await SetStatusAsync(StatusIdle);
            }
        }

        private Task<T> GetAsync<T>(string path) where T : class => SendAsync<T>(HttpMethod.Get, path)!;
        private Task<T> PostAsync<T>(string path, object data) where T : class => SendAsync<T>(HttpMethod.Post, path, data);
        private Task<T> PutAsync<T>(string path, object data) where T : class => SendAsync<T>(HttpMethod.Put, path, data);

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
                throw new HttpException(response.StatusCode);

            var responseContent = await response.Content.ReadAsStringAsync();
            if (responseContent.Length == 0)
                return null!;

            return JsonSerializer.Deserialize<T>(responseContent, _options)!;
        }

        private async Task<T> PostFormAsync<T>(string path, Dictionary<string, object> formData) where T : class
        {
            var url = GetUri(path);
            using var form = new MultipartFormDataContent();
            foreach (var kvp in formData)
            {
                if (kvp.Value is byte[] b)
                {
                    form.Add(new ByteArrayContent(b), kvp.Key, kvp.Key);
                }
                else
                {
                    form.Add(new StringContent(kvp.Value.ToString()), kvp.Key);
                }
            }

            var response = await _httpClient.PostAsync(url, form);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"{response.StatusCode} returned");

            var responseContent = await response.Content.ReadAsStringAsync();
            if (responseContent.Length == 0)
                return null!;

            return JsonSerializer.Deserialize<T>(responseContent, _options)!;
        }

        private string GetUri(string path)
        {
            return $"{BaseUri}/{path}";
        }

        private class RegisterResponse
        {
            public Guid Id { get; set; }
        }

        public class QueueResponseItem
        {
            public int Id { get; set; }
            public int GameId { get; set; }
            public DateTime Created { get; set; }
            public string[] UserTags { get; set; } = [];
            public int UserId { get; set; }
            public int Seed { get; set; }
            public int ConfigId { get; set; }
            public int Status { get; set; }
            public int UserRole { get; set; }
            public string? UserName { get; set; }
            public int ProfileId { get; set; }
            public string? ProfileName { get; set; }
            public string? ProfileDescription { get; set; }
            public int ProfileUserId { get; set; }
            public string? ProfileUserName { get; set; }
            public string? Config { get; set; }
        }

        private class HttpException(HttpStatusCode code) : Exception($"{code} returned from http request")
        {
        }

        private class UnixDateTimeConverter : JsonConverter<DateTime>
        {
            public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                return reader.GetInt64().ToDateTime();
            }

            public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
            {
                writer.WriteNumberValue(value.ToUnixTimeSeconds());
            }
        }
    }
}
