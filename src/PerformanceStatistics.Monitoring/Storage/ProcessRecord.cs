using System;

namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// A process seen by a history store, with its identity and lifetime in the store.
    /// </summary>
    public class ProcessRecord
    {
        #region Public-Members

        /// <summary>
        /// Process key (pid@startTicks).
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Process ID.
        /// </summary>
        public int Pid { get; set; } = 0;

        /// <summary>
        /// Parent process ID.
        /// </summary>
        public int? ParentPid { get; set; } = null;

        /// <summary>
        /// Name.
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
        /// User.
        /// </summary>
        public string User { get; set; } = string.Empty;

        /// <summary>
        /// Start time (UTC).
        /// </summary>
        public DateTime? StartTimeUtc { get; set; } = null;

        /// <summary>
        /// First time seen (UTC).
        /// </summary>
        public DateTime FirstSeenUtc { get; set; }

        /// <summary>
        /// Last time seen (UTC). A process not seen in the latest snapshot has exited.
        /// </summary>
        public DateTime LastSeenUtc { get; set; }

        #endregion
    }
}
