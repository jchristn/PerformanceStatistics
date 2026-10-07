using System;

namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// Static facts about a monitored host.
    /// </summary>
    public class HostInfo
    {
        #region Public-Members

        /// <summary>
        /// Stable host identifier (defaults to the machine name, lower-cased).
        /// </summary>
        public string HostId { get; set; } = string.Empty;

        /// <summary>
        /// Machine name.
        /// </summary>
        public string HostName { get; set; } = string.Empty;

        /// <summary>
        /// Platform: Windows, Linux, or Mac.
        /// </summary>
        public string Platform { get; set; } = string.Empty;

        /// <summary>
        /// Operating system description.
        /// </summary>
        public string OperatingSystem { get; set; } = string.Empty;

        /// <summary>
        /// Processor architecture.
        /// </summary>
        public string Architecture { get; set; } = string.Empty;

        /// <summary>
        /// CPU model, or null.
        /// </summary>
        public string CpuModel { get; set; } = null;

        /// <summary>
        /// Logical processors.
        /// </summary>
        public int LogicalProcessors { get; set; } = 0;

        /// <summary>
        /// Total physical memory in bytes, or null.
        /// </summary>
        public long? TotalMemoryBytes { get; set; } = null;

        /// <summary>
        /// Boot time (UTC), or null.
        /// </summary>
        public DateTime? BootTimeUtc { get; set; } = null;

        /// <summary>
        /// True if the collector runs elevated (Administrator or root) and can read every process.
        /// </summary>
        public bool Elevated { get; set; } = false;

        /// <summary>
        /// Version of the collecting software.
        /// </summary>
        public string CollectorVersion { get; set; } = string.Empty;

        #endregion
    }
}
