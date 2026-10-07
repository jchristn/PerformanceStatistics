using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PerformanceStatistics.Monitoring;

namespace TaskManager.Agent
{
    /// <summary>
    /// The agent side of the server API (X-Agent-Key authenticated, gzip request bodies).
    /// </summary>
    public sealed class AgentClient : IDisposable
    {
        #region Private-Members

        private readonly HttpClient _Http;

        #endregion

        #region Constructors-and-Factories

        public AgentClient(string serverUrl, string agentKey)
        {
            if (String.IsNullOrWhiteSpace(serverUrl)) throw new ArgumentNullException(nameof(serverUrl));
            _Http = new HttpClient { BaseAddress = new Uri(serverUrl.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(20) };
            _Http.DefaultRequestHeaders.Add("X-Agent-Key", agentKey ?? string.Empty);
        }

        #endregion

        #region Public-Methods

        public Task HelloAsync(AgentHello hello, CancellationToken token)
        {
            return PostAsync("api/v1/agent/hello", hello, token);
        }

        public Task IngestAsync(List<HostSnapshot> snapshots, CancellationToken token)
        {
            return PostAsync("api/v1/agent/ingest", snapshots, token);
        }

        public async Task<List<ProcessActionRequest>> GetActionsAsync(string hostId, CancellationToken token)
        {
            using (HttpResponseMessage resp = await _Http.GetAsync("api/v1/agent/actions?host=" + Uri.EscapeDataString(hostId), token).ConfigureAwait(false))
            {
                await EnsureAsync(resp, token).ConfigureAwait(false);
                return MonitoringJson.Deserialize<List<ProcessActionRequest>>(await resp.Content.ReadAsStringAsync(token).ConfigureAwait(false)) ?? new List<ProcessActionRequest>();
            }
        }

        public Task PostResultsAsync(string hostId, List<ProcessActionResponse> results, CancellationToken token)
        {
            return PostAsync("api/v1/agent/actions/results?host=" + Uri.EscapeDataString(hostId), results, token);
        }

        public void Dispose()
        {
            _Http.Dispose();
        }

        #endregion

        #region Private-Methods

        private async Task PostAsync(string path, object body, CancellationToken token)
        {
            byte[] json = Encoding.UTF8.GetBytes(MonitoringJson.Serialize(body));
            byte[] compressed;
            using (MemoryStream ms = new MemoryStream())
            {
                using (GZipStream gz = new GZipStream(ms, CompressionLevel.Fastest, true)) gz.Write(json, 0, json.Length);
                compressed = ms.ToArray();
            }

            using (ByteArrayContent content = new ByteArrayContent(compressed))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
                content.Headers.ContentEncoding.Add("gzip");
                using (HttpResponseMessage resp = await _Http.PostAsync(path, content, token).ConfigureAwait(false))
                {
                    await EnsureAsync(resp, token).ConfigureAwait(false);
                }
            }
        }

        private static async Task EnsureAsync(HttpResponseMessage resp, CancellationToken token)
        {
            if (resp.IsSuccessStatusCode) return;
            string text = await resp.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            throw new HttpRequestException("HTTP " + (int)resp.StatusCode + ": " + text, null, resp.StatusCode);
        }

        #endregion
    }
}
