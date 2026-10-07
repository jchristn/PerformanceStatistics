using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PerformanceStatistics.Monitoring;

namespace TaskManager.Server
{
    /// <summary>
    /// Initializes storage, seeds hosts from history, and compacts history every minute.
    /// </summary>
    public sealed class StartupService : BackgroundService
    {
        #region Private-Members

        private readonly IHistoryStore _Store;
        private readonly AuditLog _Audit;
        private readonly HostRegistry _Registry;
        private readonly ILogger<StartupService> _Logger;
        private readonly TaskCompletionSource<bool> _Ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        #endregion

        #region Constructors-and-Factories

        public StartupService(IHistoryStore store, AuditLog audit, HostRegistry registry, ILogger<StartupService> logger)
        {
            _Store = store;
            _Audit = audit;
            _Registry = registry;
            _Logger = logger;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Completes when storage is ready.
        /// </summary>
        public Task Ready
        {
            get { return _Ready.Task; }
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await _Store.InitializeAsync(cancellationToken).ConfigureAwait(false);
            await _Audit.InitializeAsync(cancellationToken).ConfigureAwait(false);
            foreach (HostRecord host in await _Store.GetHostsAsync(cancellationToken).ConfigureAwait(false)) _Registry.Seed(host);
            _Ready.TrySetResult(true);
            await base.StartAsync(cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region Protected-Methods

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken).ConfigureAwait(false);
                    int deleted = await _Store.CompactAsync(DateTime.UtcNow, stoppingToken).ConfigureAwait(false);
                    if (deleted > 0) _Logger.LogDebug("Compaction removed {Rows} rows", deleted);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception e)
                {
                    _Logger.LogWarning(e, "History compaction failed");
                }
            }
        }

        #endregion
    }
}
