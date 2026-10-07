using System;
using System.Diagnostics;

namespace PerformanceStatistics.Windows
{
    /// <summary>
    /// Windows process counters. Every field comes from <see cref="Process"/>; handle count is the number of
    /// open kernel handles. CPU utilization uses process CPU time (not a per-name performance counter
    /// instance), so processes sharing a name are measured individually.
    /// </summary>
    public class WindowsProcessCounters : ProcessCountersBase
    {
        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate. Takes ownership of <paramref name="process"/>.
        /// </summary>
        /// <param name="process">Process.</param>
        /// <exception cref="ArgumentNullException">process is null.</exception>
        public WindowsProcessCounters(Process process) : base(process)
        {
        }

        #endregion
    }
}
