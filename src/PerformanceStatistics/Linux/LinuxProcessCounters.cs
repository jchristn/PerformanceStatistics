using System;
using System.Diagnostics;

namespace PerformanceStatistics.Linux
{
    /// <summary>
    /// Linux process counters. Handle count is the number of open file descriptors in /proc/&lt;pid&gt;/fd
    /// (null for other users' processes without privileges). Windows-only paged memory fields are null.
    /// </summary>
    public class LinuxProcessCounters : ProcessCountersBase
    {
        #region Public-Members

        /// <summary>
        /// Open file descriptor count, or null if unreadable.
        /// </summary>
        public override int? HandleCount
        {
            get
            {
                int? pid = ProcessId;
                return pid.HasValue ? ProcFileParser.GetProcessFdCount(pid.Value) : null;
            }
        }

        /// <summary>
        /// Not available on Linux; always null.
        /// </summary>
        public override long? NonPagedSystemMemory
        {
            get { return null; }
        }

        /// <summary>
        /// Not available on Linux; always null.
        /// </summary>
        public override long? PagedSystemMemory
        {
            get { return null; }
        }

        /// <summary>
        /// Not available on Linux; always null.
        /// </summary>
        public override long? PeakPagedSystemMemory
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
        public LinuxProcessCounters(Process process) : base(process)
        {
        }

        #endregion
    }
}
