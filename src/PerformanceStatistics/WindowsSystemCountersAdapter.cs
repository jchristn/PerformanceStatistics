using System;

namespace PerformanceStatistics
{
    /// <summary>
    /// Adapter for WindowsSystemCounters to implement ISystemCounters.
    /// </summary>
    internal class WindowsSystemCountersAdapter : ISystemCounters
    {
        private readonly WindowsSystemCounters _inner;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="inner">The WindowsSystemCounters instance to wrap.</param>
        public WindowsSystemCountersAdapter(WindowsSystemCounters inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        /// <summary>
        /// CPU utilization percentage.
        /// </summary>
        public double? CpuUtilizationPercent => _inner.CpuUtilizationPercent;

        /// <summary>
        /// Memory free in megabytes.
        /// </summary>
        public double? MemoryFreeMegabytes => _inner.MemoryFreeMegabytes;

        /// <summary>
        /// Total disk read operations per second.
        /// </summary>
        public int? TotalDiskReadOperations => _inner.TotalDiskReadOperations;

        /// <summary>
        /// Total disk write operations per second.
        /// </summary>
        public int? TotalDiskWriteOperations => _inner.TotalDiskWriteOperations;

        /// <summary>
        /// Total disk read queue length.
        /// </summary>
        public int? TotalDiskReadQueue => _inner.TotalDiskReadQueue;

        /// <summary>
        /// Total disk write queue length.
        /// </summary>
        public int? TotalDiskWriteQueue => _inner.TotalDiskWriteQueue;

        /// <summary>
        /// Total disk free percentage.
        /// </summary>
        public double? TotalDiskFreePercent => _inner.TotalDiskFreePercent;

        /// <summary>
        /// Total disk free megabytes.
        /// </summary>
        public double? TotalDiskFreeMegabytes => _inner.TotalDiskFreeMegabytes;

        /// <summary>
        /// Total disk size megabytes.
        /// </summary>
        public double? TotalDiskSizeMegabytes => _inner.TotalDiskSizeMegabytes;

        /// <summary>
        /// Total disk used megabytes.
        /// </summary>
        public double? TotalDiskUsedMegabytes => _inner.TotalDiskUsedMegabytes;
    }
}
