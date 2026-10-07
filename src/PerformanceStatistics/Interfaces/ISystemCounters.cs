using System;

namespace PerformanceStatistics
{
    /// <summary>
    /// Interface for system-level performance counters.
    /// </summary>
    public interface ISystemCounters
    {
        /// <summary>
        /// CPU utilization percentage (0-100).
        /// Returns null if the metric is unavailable on this platform.
        /// Averaged over the interval since the previous read; the first read waits briefly for a second sample.
        /// </summary>
        double? CpuUtilizationPercent { get; }

        /// <summary>
        /// Available/free memory in megabytes.
        /// Returns null if the metric is unavailable on this platform.
        /// </summary>
        double? MemoryFreeMegabytes { get; }

        /// <summary>
        /// Total disk read operations per second across all disks.
        /// Returns null if the metric is unavailable on this platform.
        /// </summary>
        int? TotalDiskReadOperations { get; }

        /// <summary>
        /// Total disk write operations per second across all disks.
        /// Returns null if the metric is unavailable on this platform.
        /// </summary>
        int? TotalDiskWriteOperations { get; }

        /// <summary>
        /// Average disk read queue length.
        /// Returns null if the metric is unavailable on this platform (e.g., macOS).
        /// Linux reports in-flight requests from /sys/block/&lt;disk&gt;/inflight.
        /// </summary>
        int? TotalDiskReadQueue { get; }

        /// <summary>
        /// Average disk write queue length.
        /// Returns null if the metric is unavailable on this platform (e.g., macOS).
        /// Linux reports in-flight requests from /sys/block/&lt;disk&gt;/inflight.
        /// </summary>
        int? TotalDiskWriteQueue { get; }

        /// <summary>
        /// Percentage of free disk space across all disks (0-100).
        /// Returns null if the metric is unavailable on this platform.
        /// </summary>
        double? TotalDiskFreePercent { get; }

        /// <summary>
        /// Free disk space in megabytes across all disks.
        /// Returns null if the metric is unavailable on this platform.
        /// </summary>
        double? TotalDiskFreeMegabytes { get; }

        /// <summary>
        /// Total disk size in megabytes across all disks.
        /// Returns null if the metric is unavailable on this platform.
        /// </summary>
        double? TotalDiskSizeMegabytes { get; }

        /// <summary>
        /// Used disk space in megabytes across all disks.
        /// Returns null if the metric is unavailable on this platform.
        /// </summary>
        double? TotalDiskUsedMegabytes { get; }
    }
}
