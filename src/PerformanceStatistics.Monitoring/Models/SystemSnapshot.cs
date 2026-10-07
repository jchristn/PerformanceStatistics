namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// System-wide metrics at one instant. Rates are averages over the interval since the previous snapshot.
    /// Null means unavailable on this platform.
    /// </summary>
    public class SystemSnapshot
    {
        #region Public-Members

        /// <summary>
        /// Total CPU utilization, 0-100.
        /// </summary>
        public double? CpuPercent { get; set; } = null;

        /// <summary>
        /// Per logical processor CPU utilization, 0-100.
        /// </summary>
        public double[] PerCoreCpuPercent { get; set; } = new double[0];

        /// <summary>
        /// Physical memory capacity in bytes.
        /// </summary>
        public long? MemoryTotalBytes { get; set; } = null;

        /// <summary>
        /// Physical memory in use in bytes.
        /// </summary>
        public long? MemoryUsedBytes { get; set; } = null;

        /// <summary>
        /// Memory available without swapping, in bytes.
        /// </summary>
        public long? MemoryAvailableBytes { get; set; } = null;

        /// <summary>
        /// Swap capacity in bytes.
        /// </summary>
        public long? SwapTotalBytes { get; set; } = null;

        /// <summary>
        /// Swap in use in bytes.
        /// </summary>
        public long? SwapUsedBytes { get; set; } = null;

        /// <summary>
        /// Disk read operations per second.
        /// </summary>
        public double? DiskReadOpsPerSecond { get; set; } = null;

        /// <summary>
        /// Disk write operations per second.
        /// </summary>
        public double? DiskWriteOpsPerSecond { get; set; } = null;

        /// <summary>
        /// Disk bytes read per second.
        /// </summary>
        public double? DiskReadBytesPerSecond { get; set; } = null;

        /// <summary>
        /// Disk bytes written per second.
        /// </summary>
        public double? DiskWriteBytesPerSecond { get; set; } = null;

        /// <summary>
        /// Disk read queue length (null on macOS).
        /// </summary>
        public double? DiskReadQueue { get; set; } = null;

        /// <summary>
        /// Disk write queue length (null on macOS).
        /// </summary>
        public double? DiskWriteQueue { get; set; } = null;

        /// <summary>
        /// Disk capacity across counted volumes, in bytes.
        /// </summary>
        public long? DiskTotalBytes { get; set; } = null;

        /// <summary>
        /// Available disk space across counted volumes, in bytes.
        /// </summary>
        public long? DiskFreeBytes { get; set; } = null;

        /// <summary>
        /// Network bytes received per second.
        /// </summary>
        public double? NetworkReceivedBytesPerSecond { get; set; } = null;

        /// <summary>
        /// Network bytes sent per second.
        /// </summary>
        public double? NetworkSentBytesPerSecond { get; set; } = null;

        /// <summary>
        /// One-minute load average (null on Windows).
        /// </summary>
        public double? LoadAverage1 { get; set; } = null;

        /// <summary>
        /// Five-minute load average (null on Windows).
        /// </summary>
        public double? LoadAverage5 { get; set; } = null;

        /// <summary>
        /// Fifteen-minute load average (null on Windows).
        /// </summary>
        public double? LoadAverage15 { get; set; } = null;

        /// <summary>
        /// Number of visible processes.
        /// </summary>
        public int? ProcessCount { get; set; } = null;

        /// <summary>
        /// Total threads across visible processes.
        /// </summary>
        public long? ThreadCount { get; set; } = null;

        /// <summary>
        /// Total handles / file descriptors across processes that report them.
        /// </summary>
        public long? HandleCount { get; set; } = null;

        /// <summary>
        /// Seconds since boot.
        /// </summary>
        public double? UptimeSeconds { get; set; } = null;

        #endregion
    }
}
