using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// Client for the TaskManager server API (used by the TUI's remote mode and by tests).
    /// </summary>
    public sealed class MonitoringClient : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Server base URL, e.g. http://localhost:8080.
        /// </summary>
        public Uri BaseUri { get; private set; }

        /// <summary>
        /// Bearer token, set by <see cref="LoginAsync"/> or directly.
        /// </summary>
        public string Token { get; set; } = null;

        #endregion

        #region Private-Members

        private readonly HttpClient _Http;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="baseUrl">Server base URL.</param>
        /// <param name="token">Optional bearer token.</param>
        public MonitoringClient(string baseUrl, string token = null)
        {
            if (String.IsNullOrWhiteSpace(baseUrl)) throw new ArgumentNullException(nameof(baseUrl));
            BaseUri = new Uri(baseUrl.TrimEnd('/') + "/");
            Token = token;
            _Http = new HttpClient { BaseAddress = BaseUri, Timeout = TimeSpan.FromSeconds(30) };
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Log in and keep the token.
        /// </summary>
        /// <param name="username">User.</param>
        /// <param name="password">Password.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Session.</returns>
        public async Task<LoginResponse> LoginAsync(string username, string password, CancellationToken token = default)
        {
            LoginResponse session = await SendAsync<LoginResponse>(HttpMethod.Post, "api/v1/auth/login", new LoginRequest { Username = username, Password = password }, token).ConfigureAwait(false);
            Token = session.Token;
            return session;
        }

        /// <summary>
        /// Hosts.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Hosts.</returns>
        public Task<List<HostStatus>> GetHostsAsync(CancellationToken token = default)
        {
            return SendAsync<List<HostStatus>>(HttpMethod.Get, "api/v1/hosts", null, token);
        }

        /// <summary>
        /// Latest snapshot of a host.
        /// </summary>
        /// <param name="hostId">Host.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Snapshot.</returns>
        public Task<HostSnapshot> GetSnapshotAsync(string hostId, CancellationToken token = default)
        {
            return SendAsync<HostSnapshot>(HttpMethod.Get, "api/v1/hosts/" + Uri.EscapeDataString(hostId) + "/snapshot", null, token);
        }

        /// <summary>
        /// The metric catalog.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Metrics.</returns>
        public Task<List<MetricDefinition>> GetCatalogAsync(CancellationToken token = default)
        {
            return SendAsync<List<MetricDefinition>>(HttpMethod.Get, "api/v1/catalog", null, token);
        }

        /// <summary>
        /// A system series.
        /// </summary>
        /// <param name="hostId">Host.</param>
        /// <param name="metricId">Metric.</param>
        /// <param name="fromUtc">Start.</param>
        /// <param name="toUtc">End.</param>
        /// <param name="stepSeconds">Bucket size.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Points.</returns>
        public Task<List<SeriesPoint>> GetHistoryAsync(string hostId, string metricId, DateTime fromUtc, DateTime toUtc, int stepSeconds, CancellationToken token = default)
        {
            return SendAsync<List<SeriesPoint>>(HttpMethod.Get, "api/v1/hosts/" + Uri.EscapeDataString(hostId) + "/history" + Range(metricId, fromUtc, toUtc, stepSeconds), null, token);
        }

        /// <summary>
        /// A process series.
        /// </summary>
        /// <param name="hostId">Host.</param>
        /// <param name="processKey">Process key.</param>
        /// <param name="metricId">Metric.</param>
        /// <param name="fromUtc">Start.</param>
        /// <param name="toUtc">End.</param>
        /// <param name="stepSeconds">Bucket size.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Points.</returns>
        public Task<List<SeriesPoint>> GetProcessHistoryAsync(string hostId, string processKey, string metricId, DateTime fromUtc, DateTime toUtc, int stepSeconds, CancellationToken token = default)
        {
            return SendAsync<List<SeriesPoint>>(HttpMethod.Get, "api/v1/hosts/" + Uri.EscapeDataString(hostId) + "/processes/" + Uri.EscapeDataString(processKey) + "/history" + Range(metricId, fromUtc, toUtc, stepSeconds), null, token);
        }

        /// <summary>
        /// Accounting report.
        /// </summary>
        /// <param name="hostId">Host.</param>
        /// <param name="fromUtc">Start.</param>
        /// <param name="toUtc">End.</param>
        /// <param name="groupBy">Grouping.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rows.</returns>
        public Task<List<AccountingRow>> GetAccountingAsync(string hostId, DateTime fromUtc, DateTime toUtc, AccountingGroupByEnum groupBy, CancellationToken token = default)
        {
            string query = "?from=" + Uri.EscapeDataString(fromUtc.ToString("o", CultureInfo.InvariantCulture)) +
                "&to=" + Uri.EscapeDataString(toUtc.ToString("o", CultureInfo.InvariantCulture)) + "&groupBy=" + groupBy;
            return SendAsync<List<AccountingRow>>(HttpMethod.Get, "api/v1/hosts/" + Uri.EscapeDataString(hostId) + "/accounting" + query, null, token);
        }

        /// <summary>
        /// Request a process action (admin only). The agent applies it on its next poll.
        /// </summary>
        /// <param name="request">Request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Pending response.</returns>
        public Task<ProcessActionResponse> RequestActionAsync(ProcessActionRequest request, CancellationToken token = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return SendAsync<ProcessActionResponse>(HttpMethod.Post, "api/v1/hosts/" + Uri.EscapeDataString(request.HostId) + "/actions", request, token);
        }

        /// <summary>
        /// Status of a requested action.
        /// </summary>
        /// <param name="hostId">Host.</param>
        /// <param name="requestId">Request ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response.</returns>
        public Task<ProcessActionResponse> GetActionAsync(string hostId, string requestId, CancellationToken token = default)
        {
            return SendAsync<ProcessActionResponse>(HttpMethod.Get, "api/v1/hosts/" + Uri.EscapeDataString(hostId) + "/actions/" + Uri.EscapeDataString(requestId), null, token);
        }

        /// <summary>
        /// Stream live snapshots of a host over WebSocket until cancelled. Reconnects after transient failures.
        /// </summary>
        /// <param name="hostId">Host.</param>
        /// <param name="onSnapshot">Callback per snapshot (on a background thread).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task StreamAsync(string hostId, Action<HostSnapshot> onSnapshot, CancellationToken token)
        {
            if (onSnapshot == null) throw new ArgumentNullException(nameof(onSnapshot));
            UriBuilder ub = new UriBuilder(new Uri(BaseUri, "api/v1/stream"));
            ub.Scheme = BaseUri.Scheme == "https" ? "wss" : "ws";
            ub.Query = "host=" + Uri.EscapeDataString(hostId) + (Token != null ? "&access_token=" + Uri.EscapeDataString(Token) : string.Empty);

            while (!token.IsCancellationRequested)
            {
                using (ClientWebSocket ws = new ClientWebSocket())
                {
                    try
                    {
                        await ws.ConnectAsync(ub.Uri, token).ConfigureAwait(false);
                        byte[] buffer = new byte[64 * 1024];
                        using (MemoryStream message = new MemoryStream())
                        {
                            while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
                            {
                                WebSocketReceiveResult result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);
                                if (result.MessageType == WebSocketMessageType.Close) break;
                                message.Write(buffer, 0, result.Count);
                                if (!result.EndOfMessage) continue;

                                HostSnapshot snapshot = JsonSerializer.Deserialize<HostSnapshot>(message.ToArray(), MonitoringJson.Options);
                                message.SetLength(0);
                                if (snapshot != null) onSnapshot(snapshot);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (WebSocketException)
                    {
                    }
                }

                try { await Task.Delay(TimeSpan.FromSeconds(2), token).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            _Http.Dispose();
        }

        #endregion

        #region Private-Methods

        private static string Range(string metricId, DateTime fromUtc, DateTime toUtc, int stepSeconds)
        {
            return "?metric=" + Uri.EscapeDataString(metricId) +
                "&from=" + Uri.EscapeDataString(fromUtc.ToString("o", CultureInfo.InvariantCulture)) +
                "&to=" + Uri.EscapeDataString(toUtc.ToString("o", CultureInfo.InvariantCulture)) +
                "&step=" + stepSeconds.ToString(CultureInfo.InvariantCulture);
        }

        private async Task<T> SendAsync<T>(HttpMethod method, string path, object body, CancellationToken token)
        {
            using (HttpRequestMessage req = new HttpRequestMessage(method, path))
            {
                if (!String.IsNullOrEmpty(Token)) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
                if (body != null) req.Content = new StringContent(MonitoringJson.Serialize(body), Encoding.UTF8, "application/json");

                using (HttpResponseMessage resp = await _Http.SendAsync(req, token).ConfigureAwait(false))
                {
                    string text = await resp.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode)
                        throw new HttpRequestException("HTTP " + (int)resp.StatusCode + " from " + path + ": " + text, null, resp.StatusCode);
                    return MonitoringJson.Deserialize<T>(text);
                }
            }
        }

        #endregion
    }
}
