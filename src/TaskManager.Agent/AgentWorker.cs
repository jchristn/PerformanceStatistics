using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PerformanceStatistics.Monitoring;

namespace TaskManager.Agent
{
    /// <summary>
    /// Samples the local machine, pushes snapshots to the server (buffering while it is unreachable), and applies
    /// process actions the server queues for this host, subject to the local policy.
    /// </summary>
    public sealed class AgentWorker : BackgroundService
    {
        #region Private-Members

        private const int MaxBatch = 30;

        private readonly AgentSettings _Settings;
        private readonly ILogger<AgentWorker> _Logger;
        private readonly object _Lock = new object();
        private readonly List<HostSnapshot> _Buffer = new List<HostSnapshot>();
        private MetricsSampler _Sampler = null;
        private ProcessActionExecutor _Executor = null;
        private bool _HelloSent = false;
        private bool _ServerReachable = true;
        private long _Dropped = 0;

        #endregion

        #region Constructors-and-Factories

        public AgentWorker(AgentSettings settings, ILogger<AgentWorker> logger)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logger = logger;
        }

        #endregion

        #region Protected-Methods

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            ProcessActionPolicy policy = _Settings.ToPolicy();
            _Executor = new ProcessActionExecutor(policy);
            _Sampler = new MetricsSampler(_Settings.HostId);
            _Sampler.Interval = TimeSpan.FromSeconds(Math.Max(0.25, _Settings.IntervalSeconds));
            _Sampler.SnapshotTaken += OnSnapshot;

            _Logger.LogInformation("Agent for host {Host} reporting to {Server} every {Interval}s; allowed actions: {Actions}; elevated: {Elevated}",
                _Sampler.HostInfo.HostId, _Settings.Server, _Sampler.Interval.TotalSeconds,
                policy.AllowedActions.Count == 0 ? "none" : String.Join(", ", policy.AllowedActions), _Sampler.HostInfo.Elevated);

            using (AgentClient client = new AgentClient(_Settings.Server, _Settings.AgentKey))
            {
                _Sampler.Start();
                Task send = SendLoopAsync(client, policy, stoppingToken);
                Task actions = ActionLoopAsync(client, stoppingToken);
                try
                {
                    await Task.WhenAll(send, actions).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    _Sampler.SnapshotTaken -= OnSnapshot;
                    _Sampler.Dispose();
                }
            }
        }

        #endregion

        #region Private-Methods

        private void OnSnapshot(object sender, HostSnapshot snapshot)
        {
            lock (_Lock)
            {
                _Buffer.Add(snapshot);
                int excess = _Buffer.Count - Math.Max(10, _Settings.BufferSize);
                if (excess > 0)
                {
                    _Buffer.RemoveRange(0, excess);
                    _Dropped += excess;
                }
            }
        }

        private async Task SendLoopAsync(AgentClient client, ProcessActionPolicy policy, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!_HelloSent)
                    {
                        await client.HelloAsync(new AgentHello { Info = _Sampler.HostInfo, AllowedActions = policy.AllowedActions.ToList() }, token).ConfigureAwait(false);
                        _HelloSent = true;
                    }

                    while (true)
                    {
                        List<HostSnapshot> batch;
                        lock (_Lock) batch = _Buffer.Take(MaxBatch).ToList();
                        if (batch.Count == 0) break;

                        await client.IngestAsync(batch, token).ConfigureAwait(false);
                        lock (_Lock) _Buffer.RemoveRange(0, Math.Min(batch.Count, _Buffer.Count));
                    }

                    if (!_ServerReachable)
                    {
                        _Logger.LogInformation("Server reachable again; {Dropped} snapshots were dropped while offline", _Dropped);
                        _ServerReachable = true;
                        _Dropped = 0;
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception e)
                {
                    if (_ServerReachable) _Logger.LogWarning("Cannot reach the server ({Message}); buffering up to {Max} snapshots", e.Message, _Settings.BufferSize);
                    _ServerReachable = false;
                    _HelloSent = false;
                }

                await Task.Delay(TimeSpan.FromSeconds(1), token).ConfigureAwait(false);
            }
        }

        private async Task ActionLoopAsync(AgentClient client, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), token).ConfigureAwait(false);
                if (!_ServerReachable || !_HelloSent) continue;

                try
                {
                    List<ProcessActionRequest> requests = await client.GetActionsAsync(_Sampler.HostInfo.HostId, token).ConfigureAwait(false);
                    if (requests.Count == 0) continue;

                    HostSnapshot latest = _Sampler.Latest;
                    List<ProcessActionResponse> results = new List<ProcessActionResponse>();
                    foreach (ProcessActionRequest r in requests)
                    {
                        ProcessActionResponse response = _Executor.Execute(r, latest?.Processes);
                        _Logger.LogInformation("Action {Action} on PID {Pid} requested by {User}: {Result}", r.Action, r.Pid, r.RequestedBy, response.Result);
                        results.Add(response);
                    }

                    await client.PostResultsAsync(_Sampler.HostInfo.HostId, results, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (HttpRequestException)
                {
                }
                catch (Exception e)
                {
                    _Logger.LogWarning(e, "Action polling failed");
                }
            }
        }

        #endregion
    }
}
