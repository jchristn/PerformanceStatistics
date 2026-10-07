using System;
using System.Collections.Generic;
using System.Linq;

namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// Integrates per-process CPU time, disk bytes, and peak memory from a stream of snapshots, grouped by process
    /// name or user. The TUI uses it for the Accounting tab; the server computes the same report from stored history.
    /// Thread-safe.
    /// </summary>
    public sealed class ProcessAccountant
    {
        #region Public-Members

        /// <summary>
        /// When accumulation started (or was last reset).
        /// </summary>
        public DateTime SinceUtc { get; private set; } = DateTime.UtcNow;

        #endregion

        #region Private-Members

        private readonly object _Lock = new object();
        private readonly int _LogicalProcessors;
        private readonly Dictionary<string, Totals> _ByName = new Dictionary<string, Totals>(StringComparer.Ordinal);
        private readonly Dictionary<string, Totals> _ByUser = new Dictionary<string, Totals>(StringComparer.Ordinal);
        private double _Seconds = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="logicalProcessors">Logical processors on the host (converts CPU % of machine to CPU-seconds).</param>
        public ProcessAccountant(int logicalProcessors)
        {
            _LogicalProcessors = Math.Max(1, logicalProcessors);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Add a snapshot. Snapshots with no interval (the first one) only register processes.
        /// </summary>
        /// <param name="snapshot">Snapshot.</param>
        public void Add(HostSnapshot snapshot)
        {
            if (snapshot == null) return;
            double interval = Math.Max(0, snapshot.IntervalSeconds);

            lock (_Lock)
            {
                _Seconds += interval;
                Accumulate(_ByName, snapshot, interval, p => String.IsNullOrEmpty(p.Name) ? "(unknown)" : p.Name);
                Accumulate(_ByUser, snapshot, interval, p => String.IsNullOrEmpty(p.User) ? "(unknown)" : p.User);
            }
        }

        /// <summary>
        /// Report, sorted by CPU-seconds descending.
        /// </summary>
        /// <param name="groupBy">Grouping.</param>
        /// <returns>Rows.</returns>
        public List<AccountingRow> Report(AccountingGroupByEnum groupBy)
        {
            lock (_Lock)
            {
                Dictionary<string, Totals> source = groupBy == AccountingGroupByEnum.User ? _ByUser : _ByName;
                return source.Select(e => new AccountingRow
                {
                    Group = e.Key,
                    CpuSeconds = Math.Round(e.Value.CpuSeconds, 2),
                    AverageCpuPercent = _Seconds > 0 ? Math.Round(e.Value.CpuSeconds / (_Seconds * _LogicalProcessors) * 100.0, 2) : 0,
                    PeakWorkingSetBytes = e.Value.PeakWorkingSet,
                    DiskReadBytes = Math.Round(e.Value.DiskRead, 0),
                    DiskWriteBytes = Math.Round(e.Value.DiskWrite, 0),
                    ProcessCount = e.Value.Keys.Count,
                    FirstSeenUtc = e.Value.FirstSeen,
                    LastSeenUtc = e.Value.LastSeen
                })
                .OrderByDescending(r => r.CpuSeconds)
                .ThenByDescending(r => r.PeakWorkingSetBytes)
                .ToList();
            }
        }

        /// <summary>
        /// Clear all totals.
        /// </summary>
        public void Reset()
        {
            lock (_Lock)
            {
                _ByName.Clear();
                _ByUser.Clear();
                _Seconds = 0;
                SinceUtc = DateTime.UtcNow;
            }
        }

        #endregion

        #region Private-Methods

        private void Accumulate(Dictionary<string, Totals> groups, HostSnapshot snapshot, double interval, Func<ProcessSnapshot, string> keyOf)
        {
            Dictionary<string, long> workingSetThisTick = new Dictionary<string, long>(StringComparer.Ordinal);

            foreach (ProcessSnapshot p in snapshot.Processes)
            {
                string group = keyOf(p);
                Totals t;
                if (!groups.TryGetValue(group, out t))
                {
                    t = new Totals { FirstSeen = snapshot.TimestampUtc };
                    groups[group] = t;
                }

                t.LastSeen = snapshot.TimestampUtc;
                t.Keys.Add(p.Key);
                if (p.CpuPercent.HasValue) t.CpuSeconds += p.CpuPercent.Value / 100.0 * _LogicalProcessors * interval;
                if (p.DiskReadBytesPerSecond.HasValue) t.DiskRead += p.DiskReadBytesPerSecond.Value * interval;
                if (p.DiskWriteBytesPerSecond.HasValue) t.DiskWrite += p.DiskWriteBytesPerSecond.Value * interval;

                long ws;
                workingSetThisTick.TryGetValue(group, out ws);
                workingSetThisTick[group] = ws + (p.WorkingSetBytes ?? 0);
            }

            foreach (KeyValuePair<string, long> e in workingSetThisTick)
            {
                Totals t = groups[e.Key];
                if (e.Value > t.PeakWorkingSet) t.PeakWorkingSet = e.Value;
            }
        }

        #endregion

        #region Private-Classes

        private sealed class Totals
        {
            public double CpuSeconds;
            public double DiskRead;
            public double DiskWrite;
            public long PeakWorkingSet;
            public DateTime FirstSeen;
            public DateTime LastSeen;
            public readonly HashSet<string> Keys = new HashSet<string>(StringComparer.Ordinal);
        }

        #endregion
    }
}
