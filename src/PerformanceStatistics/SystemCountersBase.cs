using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using PerformanceStatistics.Internal;

namespace PerformanceStatistics
{
    /// <summary>
    /// Shared implementation of <see cref="ISystemCounters"/>. Platform classes supply raw readings
    /// (cumulative CPU times, available memory, cumulative disk operations, disk queues, drive filtering);
    /// this class owns sampling, rounding, units, and formatting so that every platform reports the same
    /// metric the same way.
    /// <para>Rate metrics (CPU, disk operations) are averages over the interval since the previous read.
    /// The first read waits at most 100ms to collect a second sample. See the README for per-platform sources.</para>
    /// </summary>
    public abstract class SystemCountersBase : ISystemCounters, IDisposable
    {
        #region Public-Members

        /// <summary>
        /// System-wide CPU utilization percentage (0-100) averaged over the interval since the previous read.
        /// </summary>
        public double? CpuUtilizationPercent
        {
            get
            {
                double percent;
                if (_Disposed || !_CpuSampler.TryGet(out percent)) return null;
                return Math.Round(percent, 0);
            }
        }

        /// <summary>
        /// Memory available to start new applications without swapping, in megabytes.
        /// Linux: MemAvailable. macOS: physical memory minus app, wired, and compressed memory (as Activity Monitor computes "Memory Used").
        /// Windows: available physical memory.
        /// </summary>
        public double? MemoryFreeMegabytes
        {
            get
            {
                long bytes;
                if (_Disposed || !SafeTryGetAvailableMemoryBytes(out bytes)) return null;
                return Math.Round(bytes / BytesPerMegabyte, 0);
            }
        }

        /// <summary>
        /// Disk read operations per second across physical disks, averaged over the interval since the previous read.
        /// </summary>
        public int? TotalDiskReadOperations
        {
            get
            {
                double reads, writes;
                if (_Disposed || !SafeTryGetDiskOperations(out reads, out writes)) return null;
                return (int)Math.Round(reads, 0);
            }
        }

        /// <summary>
        /// Disk write operations per second across physical disks, averaged over the interval since the previous read.
        /// </summary>
        public int? TotalDiskWriteOperations
        {
            get
            {
                double reads, writes;
                if (_Disposed || !SafeTryGetDiskOperations(out reads, out writes)) return null;
                return (int)Math.Round(writes, 0);
            }
        }

        /// <summary>
        /// Disk read queue length. Null where the platform does not expose it (macOS).
        /// </summary>
        public int? TotalDiskReadQueue
        {
            get
            {
                double read, write;
                if (_Disposed || !SafeTryGetDiskQueues(out read, out write)) return null;
                return (int)Math.Round(read, 0);
            }
        }

        /// <summary>
        /// Disk write queue length. Null where the platform does not expose it (macOS).
        /// </summary>
        public int? TotalDiskWriteQueue
        {
            get
            {
                double read, write;
                if (_Disposed || !SafeTryGetDiskQueues(out read, out write)) return null;
                return (int)Math.Round(write, 0);
            }
        }

        /// <summary>
        /// Percentage of disk capacity that is available, across local fixed drives.
        /// </summary>
        public double? TotalDiskFreePercent
        {
            get
            {
                long free, total;
                if (!TryGetDiskSpace(out free, out total)) return null;
                return Math.Round(free / (double)total * 100.0, 0);
            }
        }

        /// <summary>
        /// Available disk space across local fixed drives, in megabytes.
        /// </summary>
        public double? TotalDiskFreeMegabytes
        {
            get
            {
                long free, total;
                if (!TryGetDiskSpace(out free, out total)) return null;
                return Math.Round(free / BytesPerMegabyte, 0);
            }
        }

        /// <summary>
        /// Disk capacity across local fixed drives, in megabytes.
        /// </summary>
        public double? TotalDiskSizeMegabytes
        {
            get
            {
                long free, total;
                if (!TryGetDiskSpace(out free, out total)) return null;
                return Math.Round(total / BytesPerMegabyte, 0);
            }
        }

        /// <summary>
        /// Used disk space (capacity minus available) across local fixed drives, in megabytes.
        /// </summary>
        public double? TotalDiskUsedMegabytes
        {
            get
            {
                long free, total;
                if (!TryGetDiskSpace(out free, out total)) return null;
                return Math.Round((total - free) / BytesPerMegabyte, 0);
            }
        }

        #endregion

        #region Private-Members

        private const double BytesPerMegabyte = 1024.0 * 1024.0;

        private readonly DeltaSampler<CpuTimes, double> _CpuSampler;
        private readonly DeltaSampler<DiskIoTotals, DiskIoRates> _DiskSampler;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate, verifying that the current operating system is supported by the derived class.
        /// </summary>
        /// <param name="platform">Operating system the derived class supports.</param>
        /// <exception cref="PlatformNotSupportedException">The current operating system is a different one.</exception>
        internal SystemCountersBase(OSPlatform platform)
        {
            if (!RuntimeInformation.IsOSPlatform(platform))
                throw new PlatformNotSupportedException(GetType().Name + " is only supported on " + platform + ".");

            _CpuSampler = new DeltaSampler<CpuTimes, double>(
                SafeTryReadCpuTimes,
                (prev, curr, elapsed) => CpuTimes.BusyPercent(prev, curr),
                (prev, curr) => curr.Total > prev.Total);
            _DiskSampler = new DeltaSampler<DiskIoTotals, DiskIoRates>(SafeTryReadDiskTotals, DiskIoRates.Compute);
            _CpuSampler.Prime();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Human-readable summary of every counter.
        /// </summary>
        /// <returns>String.</returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("  CPU Utilization Percent       : " + Format(CpuUtilizationPercent) + "%" + Environment.NewLine);
            sb.Append("  Memory Free (Megabytes)       : " + Format(MemoryFreeMegabytes) + "MB" + Environment.NewLine);
            sb.Append("  Total Disk Read Operations    : " + Format(TotalDiskReadOperations) + Environment.NewLine);
            sb.Append("  Total Disk Write Operations   : " + Format(TotalDiskWriteOperations) + Environment.NewLine);
            sb.Append("  Total Disk Read Queue         : " + Format(TotalDiskReadQueue) + Environment.NewLine);
            sb.Append("  Total Disk Write Queue        : " + Format(TotalDiskWriteQueue) + Environment.NewLine);
            sb.Append("  Total Disk Free Percent       : " + Format(TotalDiskFreePercent) + "%" + Environment.NewLine);
            sb.Append("  Total Disk Free Megabytes     : " + Format(TotalDiskFreeMegabytes) + "MB" + Environment.NewLine);
            sb.Append("  Total Disk Size Megabytes     : " + Format(TotalDiskSizeMegabytes) + "MB" + Environment.NewLine);
            sb.Append("  Total Disk Used Megabytes     : " + Format(TotalDiskUsedMegabytes) + "MB" + Environment.NewLine);
            return sb.ToString();
        }

        /// <summary>
        /// Release platform resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Internal-and-Protected-Methods

        /// <summary>
        /// Read cumulative system CPU times. Must not throw.
        /// </summary>
        /// <param name="times">Times.</param>
        /// <returns>True if read.</returns>
        internal abstract bool TryReadCpuTimes(out CpuTimes times);

        /// <summary>
        /// Read available memory in bytes. Must not throw.
        /// </summary>
        /// <param name="bytes">Bytes.</param>
        /// <returns>True if read.</returns>
        internal abstract bool TryGetAvailableMemoryBytes(out long bytes);

        /// <summary>
        /// Read cumulative completed disk operations. Must not throw. The default reports unavailable.
        /// </summary>
        /// <param name="totals">Totals.</param>
        /// <returns>True if read.</returns>
        internal virtual bool TryReadDiskTotals(out DiskIoTotals totals)
        {
            totals = default(DiskIoTotals);
            return false;
        }

        /// <summary>
        /// Read disk read and write queue lengths. Must not throw. The default reports unavailable.
        /// </summary>
        /// <param name="read">Read queue length.</param>
        /// <param name="write">Write queue length.</param>
        /// <returns>True if read.</returns>
        internal virtual bool TryGetDiskQueues(out double read, out double write)
        {
            read = 0;
            write = 0;
            return false;
        }

        /// <summary>
        /// Decide whether a ready, non-empty drive counts toward disk space totals.
        /// The default accepts fixed drives.
        /// </summary>
        /// <param name="drive">Drive.</param>
        /// <returns>True to include.</returns>
        internal virtual bool IncludeDrive(DriveInfo drive)
        {
            return drive.DriveType == DriveType.Fixed;
        }

        /// <summary>
        /// Release platform resources.
        /// </summary>
        /// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
        protected virtual void Dispose(bool disposing)
        {
            _Disposed = true;
        }

        #endregion

        #region Private-Methods

        private bool SafeTryReadCpuTimes(out CpuTimes times)
        {
            try
            {
                return TryReadCpuTimes(out times);
            }
            catch
            {
                times = default(CpuTimes);
                return false;
            }
        }

        private bool SafeTryReadDiskTotals(out DiskIoTotals totals)
        {
            try
            {
                return TryReadDiskTotals(out totals);
            }
            catch
            {
                totals = default(DiskIoTotals);
                return false;
            }
        }

        private bool SafeTryGetAvailableMemoryBytes(out long bytes)
        {
            try
            {
                return TryGetAvailableMemoryBytes(out bytes) && bytes >= 0;
            }
            catch
            {
                bytes = 0;
                return false;
            }
        }

        private bool SafeTryGetDiskOperations(out double reads, out double writes)
        {
            DiskIoRates rates;
            if (_DiskSampler.TryGet(out rates))
            {
                reads = rates.ReadsPerSecond;
                writes = rates.WritesPerSecond;
                return true;
            }

            reads = 0;
            writes = 0;
            return false;
        }

        private bool SafeTryGetDiskQueues(out double read, out double write)
        {
            try
            {
                return TryGetDiskQueues(out read, out write);
            }
            catch
            {
                read = 0;
                write = 0;
                return false;
            }
        }

        private bool TryGetDiskSpace(out long free, out long total)
        {
            return DiskSpace.TryGetTotals(SafeIncludeDrive, out free, out total) && total > 0;
        }

        private bool SafeIncludeDrive(DriveInfo drive)
        {
            try
            {
                return IncludeDrive(drive);
            }
            catch
            {
                return false;
            }
        }

        private static string Format(double? value)
        {
            return value.HasValue ? value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "N/A";
        }

        private static string Format(int? value)
        {
            return value.HasValue ? value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "N/A";
        }

        #endregion
    }
}
