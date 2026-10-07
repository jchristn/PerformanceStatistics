using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PerformanceStatistics.Linux
{
    /// <summary>
    /// Linux performance statistics.
    /// </summary>
#if NET5_0_OR_GREATER
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
#endif
    public class LinuxPerformanceStatistics : PerformanceStatisticsBase
    {
        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="monitoredProcesses">Process names to monitor; may be null.</param>
        /// <exception cref="PlatformNotSupportedException">Not running on Linux.</exception>
        public LinuxPerformanceStatistics(List<string> monitoredProcesses = null)
            : base(new LinuxSystemCounters(), monitoredProcesses)
        {
        }

        #endregion

        #region Internal-Methods

        /// <inheritdoc />
        internal override ProcessCountersBase CreateProcessCounters(Process process)
        {
            return new LinuxProcessCounters(process);
        }

        #endregion
    }
}
