using System;
using System.IO;
using System.Runtime.InteropServices;
using PerformanceStatistics.Internal;

namespace PerformanceStatistics.Mac
{
    /// <summary>
    /// macOS system counters.
    /// <list type="bullet">
    /// <item>CPU: host_statistics(HOST_CPU_LOAD_INFO) tick deltas.</item>
    /// <item>Memory: hw.memsize minus app, wired, and compressed memory (host_statistics64), as Activity Monitor computes it.</item>
    /// <item>Disk operations: IOKit block storage driver statistics (via ioreg), with separate read and write counts.</item>
    /// <item>Disk queues: not exposed by macOS; always null.</item>
    /// <item>Disk space: the root volume and volumes under /Volumes; APFS volumes sharing a container are counted once.</item>
    /// </list>
    /// </summary>
#if NET5_0_OR_GREATER
    [System.Runtime.Versioning.SupportedOSPlatform("macos")]
#endif
    public class MacSystemCounters : SystemCountersBase
    {
        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <exception cref="PlatformNotSupportedException">Not running on macOS.</exception>
        public MacSystemCounters() : base(OSPlatform.OSX)
        {
        }

        #endregion

        #region Internal-Methods

        /// <inheritdoc />
        internal override bool TryReadCpuTimes(out CpuTimes times)
        {
            return MacSystemInfo.TryReadCpuTimes(out times);
        }

        /// <inheritdoc />
        internal override bool TryGetAvailableMemoryBytes(out long bytes)
        {
            return MacSystemInfo.TryGetAvailableMemoryBytes(out bytes);
        }

        /// <inheritdoc />
        internal override bool TryReadPerCoreCpuTimes(out CpuTimes[] times)
        {
            return MacSystemInfo.TryReadPerCoreCpuTimes(out times);
        }

        /// <inheritdoc />
        internal override bool TryGetTotalMemoryBytes(out long bytes)
        {
            return MacNativeMethods.TryGetSysctl("hw.memsize", out bytes);
        }

        /// <inheritdoc />
        internal override bool TryGetSwap(out long total, out long used)
        {
            return MacSystemInfo.TryGetSwap(out total, out used);
        }

        /// <inheritdoc />
        internal override bool TryGetLoadAverages(out double[] loads)
        {
            loads = new double[3];
            return UnixNativeMethods.getloadavg(loads, 3) == 3;
        }

        /// <inheritdoc />
        internal override void FillSystemInfo(SystemInfo info)
        {
            info.BootTimeUtc = MacSystemInfo.GetBootTimeUtc();
            info.CpuModel = MacSystemInfo.GetCpuModel();
        }

        /// <inheritdoc />
        internal override bool TryReadDiskTotals(out DiskIoTotals totals)
        {
            return MacSystemInfo.TryReadDiskTotals(out totals);
        }

        /// <inheritdoc />
        internal override bool IncludeDrive(DriveInfo drive)
        {
            // System volumes under /System/Volumes share the root's APFS container, and disk images and
            // simulator runtimes mount elsewhere; Finder shows "/" plus /Volumes/*, and so do we.
            if (drive.DriveType != DriveType.Fixed) return false;
            string name = drive.Name;
            return String.Equals(name, "/", StringComparison.Ordinal)
                || name.StartsWith("/Volumes/", StringComparison.Ordinal);
        }

        #endregion
    }
}
