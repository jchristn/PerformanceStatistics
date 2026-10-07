using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PerformanceStatistics.Windows
{
    /// <summary>
    /// Windows performance statistics.
    /// </summary>
#if NET5_0_OR_GREATER
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
#endif
    public class WindowsPerformanceStatistics : PerformanceStatisticsBase
    {
        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="monitoredProcesses">Process names to monitor (without ".exe"); may be null.</param>
        /// <exception cref="PlatformNotSupportedException">Not running on Windows.</exception>
        public WindowsPerformanceStatistics(List<string> monitoredProcesses = null)
            : base(new WindowsSystemCounters(), monitoredProcesses)
        {
        }

        #endregion

        #region Internal-Methods

        /// <inheritdoc />
        internal override ProcessCountersBase CreateProcessCounters(Process process)
        {
            return new WindowsProcessCounters(process);
        }

        #endregion
    }
}
