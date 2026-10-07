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

        #region Internal-Methods

        /// <inheritdoc />
        internal override int? ReadParentProcessId(int pid)
        {
            return WindowsNativeMethods.GetParentProcessId(pid);
        }

        /// <inheritdoc />
        internal override string ReadExecutablePath(int pid)
        {
            return WindowsNativeMethods.GetImagePath(pid);
        }

        /// <inheritdoc />
        internal override string ReadCommandLine(int pid)
        {
            return WindowsNativeMethods.GetCommandLine(pid);
        }

        /// <inheritdoc />
        internal override string ReadUserName(int pid)
        {
            return WindowsNativeMethods.GetUserName(pid);
        }

        /// <inheritdoc />
        internal override ProcessStateEnum ReadState(int pid)
        {
            // Windows has no process-level state; a process whose threads are all suspended is stopped.
            // Uses the counter's own Process (refreshed once per sampling pass) rather than a new snapshot.
            int? stopped = Read(p =>
            {
                int total = 0;
                int suspended = 0;
                foreach (ProcessThread thread in p.Threads)
                {
                    total++;
                    if (thread.ThreadState == System.Diagnostics.ThreadState.Wait && thread.WaitReason == ThreadWaitReason.Suspended) suspended++;
                }

                return total == 0 ? (int?)null : (suspended == total ? 1 : 0);
            });

            if (!stopped.HasValue) return ProcessStateEnum.Unknown;
            return stopped.Value == 1 ? ProcessStateEnum.Stopped : ProcessStateEnum.Running;
        }

        /// <inheritdoc />
        internal override bool TryReadDiskIoBytes(int pid, out long readBytes, out long writeBytes)
        {
            return WindowsNativeMethods.TryGetIoBytes(pid, out readBytes, out writeBytes);
        }

        #endregion
    }
}
