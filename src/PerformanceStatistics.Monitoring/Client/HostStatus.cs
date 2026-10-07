using System;
using System.Collections.Generic;

namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// A host as the server sees it.
    /// </summary>
    public class HostStatus
    {
        #region Public-Members

        /// <summary>
        /// Host facts.
        /// </summary>
        public HostInfo Info { get; set; } = new HostInfo();

        /// <summary>
        /// Last snapshot time (UTC).
        /// </summary>
        public DateTime? LastSeenUtc { get; set; } = null;

        /// <summary>
        /// True if a snapshot arrived recently.
        /// </summary>
        public bool Online { get; set; } = false;

        /// <summary>
        /// Actions the host's agent allows.
        /// </summary>
        public List<ProcessActionKindEnum> AllowedActions { get; set; } = new List<ProcessActionKindEnum>();

        /// <summary>
        /// Latest CPU utilization, for fleet overviews.
        /// </summary>
        public double? CpuPercent { get; set; } = null;

        /// <summary>
        /// Latest memory in use, in bytes.
        /// </summary>
        public long? MemoryUsedBytes { get; set; } = null;

        #endregion
    }
}
