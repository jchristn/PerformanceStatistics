using System;

namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// A host known to a history store.
    /// </summary>
    public class HostRecord
    {
        #region Public-Members

        /// <summary>
        /// Host facts.
        /// </summary>
        public HostInfo Info { get; set; } = new HostInfo();

        /// <summary>
        /// Last time a snapshot arrived (UTC).
        /// </summary>
        public DateTime LastSeenUtc { get; set; }

        #endregion
    }
}
