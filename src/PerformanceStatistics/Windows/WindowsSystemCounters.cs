using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using PerformanceStatistics.Internal;

namespace PerformanceStatistics.Windows
{
    /// <summary>
    /// Windows system counters.
    /// <list type="bullet">
    /// <item>CPU: GetSystemTimes deltas (independent of the localized performance counter names).</item>
    /// <item>Memory: GlobalMemoryStatusEx available physical memory.</item>
    /// <item>Disk operations: cumulative raw values of the PhysicalDisk(_Total) "Disk Reads/sec" and "Disk Writes/sec" counters.</item>
    /// <item>Disk queues: PhysicalDisk(_Total) "Avg. Disk Read/Write Queue Length", averaged since the previous read (the first read may be 0).</item>
    /// <item>Disk space: fixed drives.</item>
    /// </list>
    /// Performance counters are created on first use and reused; if they are unavailable (disabled or
    /// corrupted counter registry), the disk operation and queue properties return null.
    /// </summary>
#if NET5_0_OR_GREATER
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
#endif
    public class WindowsSystemCounters : SystemCountersBase
    {
        #region Private-Members

        private const string DiskCategory = "PhysicalDisk";
        private const string TotalInstance = "_Total";

        private readonly object _CounterLock = new object();
        private bool _CountersInitialized = false;
        private PerformanceCounter _DiskReads = null;
        private PerformanceCounter _DiskWrites = null;
        private PerformanceCounter _DiskReadBytes = null;
        private PerformanceCounter _DiskWriteBytes = null;
        private PerformanceCounter _DiskReadQueue = null;
        private PerformanceCounter _DiskWriteQueue = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <exception cref="PlatformNotSupportedException">Not running on Windows.</exception>
        public WindowsSystemCounters() : base(OSPlatform.Windows)
        {
        }

        #endregion

        #region Internal-and-Protected-Methods

        /// <inheritdoc />
        internal override bool TryReadCpuTimes(out CpuTimes times)
        {
            times = default(CpuTimes);
            long idle, kernel, user;
            if (!WindowsNativeMethods.GetSystemTimes(out idle, out kernel, out user)) return false;

            // Kernel time includes idle time.
            times = new CpuTimes((ulong)(kernel + user), (ulong)idle);
            return true;
        }

        /// <inheritdoc />
        internal override bool TryGetAvailableMemoryBytes(out long bytes)
        {
            bytes = 0;
            WindowsNativeMethods.MEMORYSTATUSEX status;
            if (!WindowsNativeMethods.TryGetMemoryStatus(out status)) return false;
            bytes = (long)status.ullAvailPhys;
            return true;
        }

        /// <inheritdoc />
        internal override bool TryGetTotalMemoryBytes(out long bytes)
        {
            long? total = WindowsNativeMethods.GetTotalPhysicalMemory();
            bytes = total ?? 0;
            return total.HasValue;
        }

        /// <inheritdoc />
        internal override bool TryGetSwap(out long total, out long used)
        {
            // Paging file beyond physical memory: commit limit minus RAM, and commit charge minus RAM in use.
            total = 0;
            used = 0;
            WindowsNativeMethods.MEMORYSTATUSEX status;
            if (!WindowsNativeMethods.TryGetMemoryStatus(out status)) return false;
            long physical = (long)status.ullTotalPhys;
            long physicalUsed = physical - (long)status.ullAvailPhys;
            long commitUsed = (long)(status.ullTotalPageFile - status.ullAvailPageFile);
            total = Math.Max(0, (long)status.ullTotalPageFile - physical);
            used = Math.Min(total, Math.Max(0, commitUsed - physicalUsed));
            return true;
        }

        /// <inheritdoc />
        internal override bool TryReadPerCoreCpuTimes(out CpuTimes[] times)
        {
            return WindowsNativeMethods.TryReadPerCoreCpuTimes(out times);
        }

        /// <inheritdoc />
        internal override void FillSystemInfo(SystemInfo info)
        {
            info.BootTimeUtc = DateTime.UtcNow - TimeSpan.FromMilliseconds(WindowsNativeMethods.GetTickCount64());
#if NET5_0_OR_GREATER
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                {
                    string name = key?.GetValue("ProcessorNameString") as string;
                    if (!String.IsNullOrWhiteSpace(name)) info.CpuModel = name.Trim();
                }
            }
            catch
            {
            }
#endif
            if (String.IsNullOrEmpty(info.CpuModel)) info.CpuModel = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
        }

        /// <inheritdoc />
        internal override bool TryReadDiskTotals(out DiskIoTotals totals)
        {
            totals = default(DiskIoTotals);
            lock (_CounterLock)
            {
                if (!EnsureCounters() || _DiskReads == null || _DiskWrites == null) return false;

                // Rate counters (PERF_COUNTER_COUNTER / PERF_COUNTER_BULK_COUNT) expose cumulative counts as RawValue.
                totals = new DiskIoTotals(
                    _DiskReads.RawValue,
                    _DiskWrites.RawValue,
                    _DiskReadBytes != null ? _DiskReadBytes.RawValue : 0,
                    _DiskWriteBytes != null ? _DiskWriteBytes.RawValue : 0);
                return true;
            }
        }

        /// <inheritdoc />
        internal override bool TryGetDiskQueues(out double read, out double write)
        {
            read = 0;
            write = 0;
            lock (_CounterLock)
            {
                if (!EnsureCounters() || _DiskReadQueue == null || _DiskWriteQueue == null) return false;
                read = _DiskReadQueue.NextValue();
                write = _DiskWriteQueue.NextValue();
                return true;
            }
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (_CounterLock)
                {
                    _DiskReads?.Dispose();
                    _DiskWrites?.Dispose();
                    _DiskReadBytes?.Dispose();
                    _DiskWriteBytes?.Dispose();
                    _DiskReadBytes = null;
                    _DiskWriteBytes = null;
                    _DiskReadQueue?.Dispose();
                    _DiskWriteQueue?.Dispose();
                    _DiskReads = null;
                    _DiskWrites = null;
                    _DiskReadQueue = null;
                    _DiskWriteQueue = null;
                }
            }

            base.Dispose(disposing);
        }

        #endregion

        #region Private-Methods

        private bool EnsureCounters()
        {
            if (_CountersInitialized) return _DiskReads != null || _DiskReadQueue != null;
            _CountersInitialized = true;

            _DiskReads = CreateCounter("Disk Reads/sec");
            _DiskWrites = CreateCounter("Disk Writes/sec");
            _DiskReadBytes = CreateCounter("Disk Read Bytes/sec");
            _DiskWriteBytes = CreateCounter("Disk Write Bytes/sec");
            _DiskReadQueue = CreateCounter("Avg. Disk Read Queue Length");
            _DiskWriteQueue = CreateCounter("Avg. Disk Write Queue Length");

            // The first NextValue() of an averaging counter always returns 0; take it now.
            try { _DiskReadQueue?.NextValue(); } catch { }
            try { _DiskWriteQueue?.NextValue(); } catch { }

            return _DiskReads != null || _DiskReadQueue != null;
        }

        private static PerformanceCounter CreateCounter(string counterName)
        {
            try
            {
                PerformanceCounter counter = new PerformanceCounter(DiskCategory, counterName, TotalInstance, true);
                // Reading RawValue validates that the category, counter, and instance exist.
                _ = counter.RawValue;
                return counter;
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}
