using System;
using System.Diagnostics;
using PerformanceStatistics.Internal;

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

        #region Internal-Methods

        /// <inheritdoc />
        internal override int? ReadParentProcessId(int pid)
        {
            int parent;
            uint status, uid;
            return MacSystemInfo.TryReadShortBsdInfo(pid, out parent, out status, out uid) ? (int?)parent : null;
        }

        /// <inheritdoc />
        internal override string ReadExecutablePath(int pid)
        {
            return MacSystemInfo.GetExecutablePath(pid);
        }

        /// <inheritdoc />
        internal override string ReadCommandLine(int pid)
        {
            return MacSystemInfo.GetCommandLine(pid);
        }

        /// <inheritdoc />
        internal override string ReadUserName(int pid)
        {
            int parent;
            uint status, uid;
            return MacSystemInfo.TryReadShortBsdInfo(pid, out parent, out status, out uid) ? UnixNativeMethods.GetUserName(uid) : string.Empty;
        }

        /// <inheritdoc />
        internal override ProcessStateEnum ReadState(int pid)
        {
            int parent;
            uint status, uid;
            if (!MacSystemInfo.TryReadShortBsdInfo(pid, out parent, out status, out uid)) return ProcessStateEnum.Unknown;
            switch (status)
            {
                case 1:
                case 2: return ProcessStateEnum.Running;
                case 3: return ProcessStateEnum.Sleeping;
                case 4: return ProcessStateEnum.Stopped;
                case 5: return ProcessStateEnum.Zombie;
                default: return ProcessStateEnum.Unknown;
            }
        }

        /// <inheritdoc />
        internal override bool TryReadDiskIoBytes(int pid, out long readBytes, out long writeBytes)
        {
            return MacSystemInfo.TryReadProcessIo(pid, out readBytes, out writeBytes);
        }

        #endregion
    }
}
