using System;

namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// Resources consumed by one group of processes (by name, or by user) over a window.
    /// </summary>
    public class AccountingRow
    {
        #region Public-Members

        /// <summary>
        /// Group key: a process name or a user name.
        /// </summary>
        public string Group { get; set; } = string.Empty;

        /// <summary>
        /// CPU time consumed in the window, in seconds (100% of one core for one second = 1).
        /// </summary>
        public double CpuSeconds { get; set; } = 0;

        /// <summary>
        /// Average CPU utilization over the window, 0-100 of machine capacity.
        /// </summary>
        public double AverageCpuPercent { get; set; } = 0;

        /// <summary>
        /// Highest combined working set observed, in bytes.
        /// </summary>
        public long PeakWorkingSetBytes { get; set; } = 0;

        /// <summary>
        /// Bytes read from disk in the window.
        /// </summary>
        public double DiskReadBytes { get; set; } = 0;

        /// <summary>
        /// Bytes written to disk in the window.
        /// </summary>
        public double DiskWriteBytes { get; set; } = 0;

        /// <summary>
        /// Distinct processes seen.
        /// </summary>
        public int ProcessCount { get; set; } = 0;

        /// <summary>
        /// First time the group was seen in the window.
        /// </summary>
        public DateTime FirstSeenUtc { get; set; }

        /// <summary>
        /// Last time the group was seen in the window.
        /// </summary>
        public DateTime LastSeenUtc { get; set; }

        #endregion
    }
}
