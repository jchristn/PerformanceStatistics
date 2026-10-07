using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// Persistent history of host snapshots with downsampling.
    /// </summary>
    public interface IHistoryStore : IDisposable
    {
        /// <summary>
        /// Create the schema if needed.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task InitializeAsync(CancellationToken token = default);

        /// <summary>
        /// Record host facts.
        /// </summary>
        /// <param name="info">Host facts.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task UpsertHostAsync(HostInfo info, CancellationToken token = default);

        /// <summary>
        /// Known hosts.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Hosts.</returns>
        Task<List<HostRecord>> GetHostsAsync(CancellationToken token = default);

        /// <summary>
        /// Store a snapshot (system, interfaces, and processes per the recording policy).
        /// </summary>
        /// <param name="snapshot">Snapshot.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task WriteAsync(HostSnapshot snapshot, CancellationToken token = default);

        /// <summary>
        /// Query a system series, bucketed by <paramref name="stepSeconds"/>.
        /// </summary>
        /// <param name="hostId">Host.</param>
        /// <param name="metricId">Metric (including "system.cpu.core.N").</param>
        /// <param name="fromUtc">Start.</param>
        /// <param name="toUtc">End.</param>
        /// <param name="stepSeconds">Bucket size; also selects the stored resolution.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Points.</returns>
        Task<List<SeriesPoint>> QuerySystemAsync(string hostId, string metricId, DateTime fromUtc, DateTime toUtc, int stepSeconds, CancellationToken token = default);

        /// <summary>
        /// Query a process series.
        /// </summary>
        /// <param name="hostId">Host.</param>
        /// <param name="processKey">Process key.</param>
        /// <param name="metricId">Metric.</param>
        /// <param name="fromUtc">Start.</param>
        /// <param name="toUtc">End.</param>
        /// <param name="stepSeconds">Bucket size.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Points.</returns>
        Task<List<SeriesPoint>> QueryProcessAsync(string hostId, string processKey, string metricId, DateTime fromUtc, DateTime toUtc, int stepSeconds, CancellationToken token = default);

        /// <summary>
        /// Query a network interface series.
        /// </summary>
        /// <param name="hostId">Host.</param>
        /// <param name="interfaceName">Interface.</param>
        /// <param name="metricId">Metric.</param>
        /// <param name="fromUtc">Start.</param>
        /// <param name="toUtc">End.</param>
        /// <param name="stepSeconds">Bucket size.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Points.</returns>
        Task<List<SeriesPoint>> QueryInterfaceAsync(string hostId, string interfaceName, string metricId, DateTime fromUtc, DateTime toUtc, int stepSeconds, CancellationToken token = default);

        /// <summary>
        /// Processes seen in a window, including exited ones.
        /// </summary>
        /// <param name="hostId">Host.</param>
        /// <param name="fromUtc">Start.</param>
        /// <param name="toUtc">End.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Processes.</returns>
        Task<List<ProcessRecord>> QueryProcessesAsync(string hostId, DateTime fromUtc, DateTime toUtc, CancellationToken token = default);

        /// <summary>
        /// Resource accounting for a window from 1-minute process aggregates.
        /// </summary>
        /// <param name="hostId">Host.</param>
        /// <param name="fromUtc">Start.</param>
        /// <param name="toUtc">End.</param>
        /// <param name="groupBy">Grouping.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rows sorted by CPU-seconds.</returns>
        Task<List<AccountingRow>> QueryAccountingAsync(string hostId, DateTime fromUtc, DateTime toUtc, AccountingGroupByEnum groupBy, CancellationToken token = default);

        /// <summary>
        /// Roll up completed minutes and hours and apply retention.
        /// </summary>
        /// <param name="nowUtc">Current time.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rows deleted.</returns>
        Task<int> CompactAsync(DateTime nowUtc, CancellationToken token = default);
    }
}
