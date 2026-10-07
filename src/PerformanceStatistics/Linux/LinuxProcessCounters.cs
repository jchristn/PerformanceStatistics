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

        #region Internal-Methods

        /// <inheritdoc />
        internal override int? ReadParentProcessId(int pid)
        {
            char state;
            int parent;
            return ProcFileParser.TryReadProcessStat(pid, out state, out parent) ? (int?)parent : null;
        }

        /// <inheritdoc />
        internal override string ReadExecutablePath(int pid)
        {
            return ProcFileParser.GetExecutablePath(pid);
        }

        /// <inheritdoc />
        internal override string ReadCommandLine(int pid)
        {
            return ProcFileParser.GetCommandLine(pid);
        }

        /// <inheritdoc />
        internal override string ReadUserName(int pid)
        {
            return ProcFileParser.GetUserName(pid);
        }

        /// <inheritdoc />
        internal override ProcessStateEnum ReadState(int pid)
        {
            char state;
            int parent;
            if (!ProcFileParser.TryReadProcessStat(pid, out state, out parent)) return ProcessStateEnum.Unknown;
            switch (state)
            {
                case 'R': return ProcessStateEnum.Running;
                case 'S':
                case 'I': return ProcessStateEnum.Sleeping;
                case 'D': return ProcessStateEnum.Waiting;
                case 'T':
                case 't': return ProcessStateEnum.Stopped;
                case 'Z':
                case 'X': return ProcessStateEnum.Zombie;
                default: return ProcessStateEnum.Unknown;
            }
        }

        /// <inheritdoc />
        internal override bool TryReadDiskIoBytes(int pid, out long readBytes, out long writeBytes)
        {
            return ProcFileParser.TryReadProcessIo(pid, out readBytes, out writeBytes);
        }

        #endregion
    }
}
