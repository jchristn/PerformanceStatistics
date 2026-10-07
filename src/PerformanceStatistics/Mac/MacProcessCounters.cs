using System;
using System.Diagnostics;

namespace PerformanceStatistics.Mac
{
    /// <summary>
    /// macOS process counters.
    /// <list type="bullet">
    /// <item>Handle count: open file descriptors via proc_pidinfo; null for other users' processes.</item>
    /// <item>Private memory: physical footprint via proc_pid_rusage (Activity Monitor's "Memory"); null for other users' processes.</item>
    /// <item>Peak working set, peak virtual, and Windows-only paged fields: not tracked by macOS; null.</item>
    /// </list>
    /// </summary>
    public class MacProcessCounters : ProcessCountersBase
    {
        #region Public-Members

        /// <summary>
        /// Open file descriptor count, or null if inaccessible.
        /// </summary>
        public override int? HandleCount
        {
            get
            {
                int? pid = ProcessId;
                return pid.HasValue ? MacSystemInfo.GetProcessFdCount(pid.Value) : null;
            }
        }

        /// <summary>
        /// Physical footprint in bytes, or null if inaccessible.
        /// </summary>
        public override long? PrivateMemory
        {
            get
            {
                int? pid = ProcessId;
                return pid.HasValue ? MacSystemInfo.GetProcessPhysicalFootprint(pid.Value) : null;
            }
        }

        /// <summary>
        /// Not available on macOS; always null.
        /// </summary>
        public override long? NonPagedSystemMemory
        {
            get { return null; }
        }

        /// <summary>
        /// Not available on macOS; always null.
        /// </summary>
        public override long? PagedSystemMemory
        {
            get { return null; }
        }

        /// <summary>
        /// Not available on macOS; always null.
        /// </summary>
        public override long? PeakPagedSystemMemory
        {
            get { return null; }
        }

        /// <summary>
        /// Not available on macOS; always null.
        /// </summary>
        public override long? PeakVirtualSystemMemory
        {
            get { return null; }
        }

        /// <summary>
        /// Not available on macOS; always null.
        /// </summary>
        public override long? PeakWorkingSetMemory
        {
            get { return null; }
        }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate. Takes ownership of <paramref name="process"/>.
        /// </summary>
        /// <param name="process">Process.</param>
        /// <exception cref="ArgumentNullException">process is null.</exception>
        public MacProcessCounters(Process process) : base(process)
        {
        }

        #endregion
    }
}
