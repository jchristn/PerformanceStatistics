using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PerformanceStatistics.Mac
{
    /// <summary>
    /// macOS performance statistics.
    /// </summary>
#if NET5_0_OR_GREATER
    [System.Runtime.Versioning.SupportedOSPlatform("macos")]
#endif
    public class MacPerformanceStatistics : PerformanceStatisticsBase
    {
        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="monitoredProcesses">Process names to monitor; may be null.</param>
        /// <exception cref="PlatformNotSupportedException">Not running on macOS.</exception>
        public MacPerformanceStatistics(List<string> monitoredProcesses = null)
            : base(new MacSystemCounters(), monitoredProcesses)
        {
        }

        #endregion

        #region Internal-Methods

        /// <inheritdoc />
        internal override ProcessCountersBase CreateProcessCounters(Process process)
        {
            return new MacProcessCounters(process);
        }

        #endregion
    }
}
