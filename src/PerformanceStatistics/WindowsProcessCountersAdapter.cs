using System;

namespace PerformanceStatistics
{
    /// <summary>
    /// Adapter for WindowsProcessCounters to implement IProcessCounters.
    /// </summary>
    internal class WindowsProcessCountersAdapter : IProcessCounters
    {
        private readonly WindowsProcessCounters _inner;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="inner">The WindowsProcessCounters instance to wrap.</param>
        public WindowsProcessCountersAdapter(WindowsProcessCounters inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        /// <summary>
        /// Process ID.
        /// </summary>
        public int? ProcessId => _inner.ProcessId;

        /// <summary>
        /// Process name.
        /// </summary>
        public string ProcessName => _inner.ProcessName;

        /// <summary>
        /// Process title.
        /// </summary>
        public string ProcessTitle => _inner.ProcessTitle;

        /// <summary>
        /// Machine name.
        /// </summary>
        public string MachineName => _inner.MachineName;

        /// <summary>
        /// CPU utilization percentage.
        /// </summary>
        public double? CpuUtilizationPercent => _inner.CpuUtilizationPercent;

        /// <summary>
        /// Handle count.
        /// </summary>
        public int? HandleCount => _inner.HandleCount;

        /// <summary>
        /// Thread count.
        /// </summary>
        public int? ThreadCount => _inner.ThreadCount;

        /// <summary>
        /// Non-paged system memory.
        /// </summary>
        public long? NonPagedSystemMemory => _inner.NonPagedSystemMemory;

        /// <summary>
        /// Paged system memory.
        /// </summary>
        public long? PagedSystemMemory => _inner.PagedSystemMemory;

        /// <summary>
        /// Private memory.
        /// </summary>
        public long? PrivateMemory => _inner.PrivateMemory;

        /// <summary>
        /// Virtual memory.
        /// </summary>
        public long? VirtualMemory => _inner.VirtualMemory;

        /// <summary>
        /// Working set memory.
        /// </summary>
        public long? WorkingSetMemory => _inner.WorkingSetMemory;

        /// <summary>
        /// Peak paged system memory.
        /// </summary>
        public long? PeakPagedSystemMemory => _inner.PeakPagedSystemMemory;

        /// <summary>
        /// Peak virtual system memory.
        /// </summary>
        public long? PeakVirtualSystemMemory => _inner.PeakVirtualSystemMemory;

        /// <summary>
        /// Peak working set memory.
        /// </summary>
        public long? PeakWorkingSetMemory => _inner.PeakWorkingSetMemory;

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        public void Dispose()
        {
            _inner.Dispose();
        }
    }
}
