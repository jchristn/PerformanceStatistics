using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PerformanceStatistics.Monitoring;

namespace TaskManager.Tui
{
    /// <summary>
    /// Streams snapshots of one host from a TaskManager server; actions go through the server to the host's agent.
    /// </summary>
    public sealed class RemoteSnapshotSource : ISnapshotSource
    {
        #region Public-Members

        /// <inheritdoc />
        public event EventHandler<HostSnapshot> SnapshotReceived;

        /// <inheritdoc />
        public HostInfo HostInfo { get; private set; }

        /// <inheritdoc />
        public string Description
        {
            get { return "server " + _Client.BaseUri.ToString().TrimEnd('/'); }
        }

        /// <inheritdoc />
        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(1);

        /// <inheritdoc />
        public bool Paused { get; set; } = false;

        /// <inheritdoc />
        public bool ActionsEnabled { get; private set; }

        #endregion

        #region Private-Members

        private readonly MonitoringClient _Client;
        private readonly CancellationTokenSource _Cts = new CancellationTokenSource();
        private Task _Stream = null;

        #endregion

        #region Constructors-and-Factories

        private RemoteSnapshotSource(MonitoringClient client, HostStatus host, bool isAdmin)
        {
            _Client = client;
            HostInfo = host.Info;
            ActionsEnabled = isAdmin && host.AllowedActions.Count > 0;
        }

        /// <summary>
        /// Connect, log in if credentials are given, and pick a host.
        /// </summary>
        /// <param name="serverUrl">Server URL.</param>
        /// <param name="hostId">Host ID; null picks the only host, or the first online host.</param>
        /// <param name="username">User, or null to use <paramref name="token"/>.</param>
        /// <param name="password">Password.</param>
        /// <param name="token">Bearer token, or null.</param>
        /// <returns>Source.</returns>
        public static async Task<RemoteSnapshotSource> ConnectAsync(string serverUrl, string hostId, string username, string password, string token)
        {
            MonitoringClient client = new MonitoringClient(serverUrl, token);
            bool isAdmin = false;
            if (!String.IsNullOrEmpty(username))
            {
                LoginResponse session = await client.LoginAsync(username, password).ConfigureAwait(false);
                isAdmin = session.IsAdmin;
            }

            System.Collections.Generic.List<HostStatus> hosts = await client.GetHostsAsync().ConfigureAwait(false);
            HostStatus host = String.IsNullOrEmpty(hostId)
                ? hosts.OrderByDescending(h => h.Online).FirstOrDefault()
                : hosts.FirstOrDefault(h => String.Equals(h.Info.HostId, hostId, StringComparison.OrdinalIgnoreCase));
            if (host == null)
                throw new InvalidOperationException(String.IsNullOrEmpty(hostId) ? "The server has no hosts yet." : "Host '" + hostId + "' is not known to the server. Known: " + String.Join(", ", hosts.Select(h => h.Info.HostId)));

            return new RemoteSnapshotSource(client, host, isAdmin);
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public void Start()
        {
            _Stream = Task.Run(() => _Client.StreamAsync(HostInfo.HostId, s =>
            {
                if (!Paused) SnapshotReceived?.Invoke(this, s);
            }, _Cts.Token));
        }

        /// <inheritdoc />
        public async Task<ProcessActionResponse> ExecuteAsync(ProcessActionRequest request)
        {
            request.HostId = HostInfo.HostId;
            ProcessActionResponse response = await _Client.RequestActionAsync(request).ConfigureAwait(false);
            for (int i = 0; i < 30 && !response.CompletedUtc.HasValue; i++)
            {
                await Task.Delay(500).ConfigureAwait(false);
                response = await _Client.GetActionAsync(HostInfo.HostId, request.Id).ConfigureAwait(false);
            }

            return response;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _Cts.Cancel();
            try { _Stream?.Wait(TimeSpan.FromSeconds(3)); } catch { }
            _Client.Dispose();
            _Cts.Dispose();
        }

        #endregion
    }
}
