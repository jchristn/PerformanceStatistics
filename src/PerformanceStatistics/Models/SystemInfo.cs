using System;

namespace PerformanceStatistics
{
    /// <summary>
    /// Static facts about the host. Values that cannot be determined are null.
    /// </summary>
    public class SystemInfo
    {
        #region Public-Members

        /// <summary>
        /// Machine (host) name.
        /// </summary>
        public string MachineName { get; set; } = string.Empty;

        /// <summary>
        /// Platform.
        /// </summary>
        public PlatformTypeEnum Platform { get; set; } = PlatformTypeEnum.Unknown;

        /// <summary>
        /// Operating system description, e.g. "Darwin 25.6.0" or "Microsoft Windows 10.0.26100".
        /// </summary>
        public string OperatingSystem { get; set; } = string.Empty;

        /// <summary>
        /// Processor architecture, e.g. "Arm64" or "X64".
        /// </summary>
        public string Architecture { get; set; } = string.Empty;

        /// <summary>
        /// CPU model name, or null if unavailable.
        /// </summary>
        public string CpuModel { get; set; } = null;

        /// <summary>
        /// Number of logical processors.
        /// </summary>
        public int LogicalProcessors { get; set; } = 0;

        /// <summary>
        /// Total physical memory in bytes, or null if unavailable.
        /// </summary>
        public long? TotalMemoryBytes { get; set; } = null;

        /// <summary>
        /// Time the system booted (UTC), or null if unavailable.
        /// </summary>
        public DateTime? BootTimeUtc { get; set; } = null;

        #endregion
    }
}
