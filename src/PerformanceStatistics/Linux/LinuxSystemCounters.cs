using System;
using System.IO;
using System.Runtime.InteropServices;
using PerformanceStatistics.Internal;

namespace PerformanceStatistics.Linux
{
    /// <summary>
    /// Linux system counters, read from /proc and /sys.
    /// <list type="bullet">
    /// <item>CPU: /proc/stat.</item>
    /// <item>Memory: /proc/meminfo MemAvailable.</item>
    /// <item>Disk operations: /proc/diskstats, physical whole disks only.</item>
    /// <item>Disk queues: in-flight requests from /sys/block/&lt;disk&gt;/inflight.</item>
    /// <item>Disk space: fixed filesystems, excluding pseudo filesystems; bind mounts are counted once.</item>
    /// </list>
    /// </summary>
#if NET5_0_OR_GREATER
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
#endif
    public class LinuxSystemCounters : SystemCountersBase
    {
        #region Private-Members

        private static readonly string[] _PseudoMountPrefixes = new string[] { "/proc", "/sys", "/dev", "/run", "/snap" };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <exception cref="PlatformNotSupportedException">Not running on Linux.</exception>
        public LinuxSystemCounters() : base(OSPlatform.Linux)
        {
        }

        #endregion

        #region Internal-Methods

        /// <inheritdoc />
        internal override bool TryReadCpuTimes(out CpuTimes times)
        {
            return ProcFileParser.TryReadCpuTimes(out times);
        }

        /// <inheritdoc />
        internal override bool TryGetAvailableMemoryBytes(out long bytes)
        {
            return ProcFileParser.TryGetAvailableMemoryBytes(out bytes);
        }

        /// <inheritdoc />
        internal override bool TryReadDiskTotals(out DiskIoTotals totals)
        {
            return ProcFileParser.TryReadDiskTotals(out totals);
        }

        /// <inheritdoc />
        internal override bool TryGetDiskQueues(out double read, out double write)
        {
            long r, w;
            bool ok = ProcFileParser.TryReadInflight(out r, out w);
            read = r;
            write = w;
            return ok;
        }

        /// <inheritdoc />
        internal override bool IncludeDrive(DriveInfo drive)
        {
            if (drive.DriveType != DriveType.Fixed) return false;

            string name = drive.Name;
            foreach (string prefix in _PseudoMountPrefixes)
            {
                if (String.Equals(name, prefix, StringComparison.Ordinal)
                    || name.StartsWith(prefix + "/", StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        #endregion
    }
}
