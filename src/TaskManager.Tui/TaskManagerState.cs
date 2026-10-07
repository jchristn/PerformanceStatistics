using System;
using System.Collections.Generic;
using System.Linq;
using PerformanceStatistics.Monitoring;

namespace TaskManager.Tui
{
    /// <summary>
    /// Everything the screens show. Owned by the UI thread: snapshots are applied through <c>app.Post</c>.
    /// </summary>
    public sealed class TaskManagerState
    {
        #region Public-Members

        /// <summary>
        /// Host facts.
        /// </summary>
        public HostInfo Host { get; private set; }

        /// <summary>
        /// Latest snapshot, or null before the first.
        /// </summary>
        public HostSnapshot Latest { get; private set; }

        /// <summary>
        /// In-memory history for charts.
        /// </summary>
        public HistoryBuffer History { get; private set; }

        /// <summary>
        /// Accounting since start (or the last reset).
        /// </summary>
        public ProcessAccountant Accountant { get; private set; }

        /// <summary>
        /// Process filter text (matches PID, name, user, path, or command line; case-insensitive).
        /// </summary>
        public string Filter { get; set; } = string.Empty;

        /// <summary>
        /// Chart window in seconds.
        /// </summary>
        public int WindowSeconds { get; set; } = 300;

        /// <summary>
        /// Accounting grouping.
        /// </summary>
        public AccountingGroupByEnum GroupBy { get; set; } = AccountingGroupByEnum.Name;

        /// <summary>
        /// Selected process key, or null.
        /// </summary>
        public string SelectedProcessKey { get; set; } = null;

        /// <summary>
        /// Selected network interface, or null.
        /// </summary>
        public string SelectedInterface { get; set; } = null;

        /// <summary>
        /// Snapshots received.
        /// </summary>
        public long SnapshotCount { get; private set; } = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="host">Host facts.</param>
        /// <param name="historyPoints">Points per system series.</param>
        public TaskManagerState(HostInfo host, int historyPoints = 900)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
            History = new HistoryBuffer(historyPoints, 300);
            Accountant = new ProcessAccountant(host.LogicalProcessors);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Apply a snapshot.
        /// </summary>
        /// <param name="snapshot">Snapshot.</param>
        public void Apply(HostSnapshot snapshot)
        {
            if (snapshot == null) return;
            Latest = snapshot;
            History.Add(snapshot);
            Accountant.Add(snapshot);
            SnapshotCount++;
        }

        /// <summary>
        /// Processes matching the filter.
        /// </summary>
        /// <returns>Processes.</returns>
        public List<ProcessSnapshot> FilteredProcesses()
        {
            if (Latest == null) return new List<ProcessSnapshot>();
            return Filtered(Latest.Processes, Filter);
        }

        /// <summary>
        /// The selected process from the latest snapshot, or null.
        /// </summary>
        /// <returns>Process.</returns>
        public ProcessSnapshot SelectedProcess()
        {
            if (Latest == null || SelectedProcessKey == null) return null;
            return Latest.Processes.FirstOrDefault(p => String.Equals(p.Key, SelectedProcessKey, StringComparison.Ordinal));
        }

        /// <summary>
        /// Filter processes by text across PID, name, user, path, and command line.
        /// </summary>
        /// <param name="processes">Processes.</param>
        /// <param name="filter">Filter text.</param>
        /// <returns>Matches.</returns>
        public static List<ProcessSnapshot> Filtered(IEnumerable<ProcessSnapshot> processes, string filter)
        {
            if (String.IsNullOrWhiteSpace(filter)) return processes.ToList();
            string f = filter.Trim();
            return processes.Where(p =>
                p.Pid.ToString(System.Globalization.CultureInfo.InvariantCulture) == f
                || Contains(p.Name, f) || Contains(p.User, f) || Contains(p.ExecutablePath, f) || Contains(p.CommandLine, f)).ToList();
        }

        /// <summary>
        /// The last <see cref="WindowSeconds"/> of a series as chart values (unavailable points become 0).
        /// </summary>
        /// <param name="series">Series.</param>
        /// <param name="intervalSeconds">Sampling interval.</param>
        /// <returns>Values.</returns>
        public List<double> Window(List<SeriesPoint> series, double intervalSeconds)
        {
            int points = (int)Math.Max(2, WindowSeconds / Math.Max(0.25, intervalSeconds));
            return series.Skip(Math.Max(0, series.Count - points)).Select(p => p.Value ?? 0).ToList();
        }

        /// <summary>
        /// The "n/a" reason for a metric on this host's platform, or null if fully available.
        /// </summary>
        /// <param name="metricId">Metric ID.</param>
        /// <returns>Reason or null.</returns>
        public string Unavailable(string metricId)
        {
            MetricDefinition m = MetricCatalog.Get(metricId);
            if (m == null) return null;
            MetricAvailability a = m.For(Host.Platform);
            return a.Level == AvailabilityEnum.Full ? null : a.Note;
        }

        #endregion

        #region Private-Methods

        private static bool Contains(string value, string filter)
        {
            return !String.IsNullOrEmpty(value) && value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        #endregion
    }
}
