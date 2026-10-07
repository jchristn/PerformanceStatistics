using System;
using System.Globalization;

namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// One process at one instant. Rates are averages over the interval since the previous snapshot.
    /// Null means unavailable (platform, or another user's process without elevation).
    /// </summary>
    public class ProcessSnapshot
    {
        #region Public-Members

        /// <summary>
        /// Stable identity across PID reuse: "pid@startTicks".
        /// </summary>
        public string Key
        {
            get
            {
                return Pid.ToString(CultureInfo.InvariantCulture) + "@" +
                    (StartTimeUtc.HasValue ? StartTimeUtc.Value.Ticks.ToString(CultureInfo.InvariantCulture) : "0");
            }
        }

        /// <summary>
        /// Process ID.
        /// </summary>
        public int Pid { get; set; } = 0;

        /// <summary>
        /// Parent process ID.
        /// </summary>
        public int? ParentPid { get; set; } = null;

        /// <summary>
        /// Process name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Executable path.
        /// </summary>
        public string ExecutablePath { get; set; } = string.Empty;

        /// <summary>
        /// Command line.
        /// </summary>
        public string CommandLine { get; set; } = string.Empty;

        /// <summary>
        /// Owning user.
        /// </summary>
        public string User { get; set; } = string.Empty;

        /// <summary>
        /// Start time (UTC).
        /// </summary>
        public DateTime? StartTimeUtc { get; set; } = null;

        /// <summary>
        /// State: Running, Sleeping, Waiting, Stopped, Zombie, or Unknown.
        /// </summary>
        public string State { get; set; } = "Unknown";

        /// <summary>
        /// Priority: Idle, BelowNormal, Normal, AboveNormal, High, RealTime, or null.
        /// </summary>
        public string Priority { get; set; } = null;

        /// <summary>
        /// CPU utilization, 0-100 of total machine capacity.
        /// </summary>
        public double? CpuPercent { get; set; } = null;

        /// <summary>
        /// Total CPU time in seconds.
        /// </summary>
        public double? CpuTimeSeconds { get; set; } = null;

        /// <summary>
        /// Working set (resident) bytes.
        /// </summary>
        public long? WorkingSetBytes { get; set; } = null;

        /// <summary>
        /// Private bytes (macOS: physical footprint).
        /// </summary>
        public long? PrivateBytes { get; set; } = null;

        /// <summary>
        /// Virtual bytes.
        /// </summary>
        public long? VirtualBytes { get; set; } = null;

        /// <summary>
        /// Disk bytes read per second.
        /// </summary>
        public double? DiskReadBytesPerSecond { get; set; } = null;

        /// <summary>
        /// Disk bytes written per second.
        /// </summary>
        public double? DiskWriteBytesPerSecond { get; set; } = null;

        /// <summary>
        /// Total disk bytes read.
        /// </summary>
        public long? DiskReadBytesTotal { get; set; } = null;

        /// <summary>
        /// Total disk bytes written.
        /// </summary>
        public long? DiskWriteBytesTotal { get; set; } = null;

        /// <summary>
        /// Handles (Windows) or open file descriptors (Linux, macOS).
        /// </summary>
        public int? HandleCount { get; set; } = null;

        /// <summary>
        /// Threads.
        /// </summary>
        public int? ThreadCount { get; set; } = null;

        #endregion
    }
}
