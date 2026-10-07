using System;
using System.Collections.Generic;

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

        /// <summary>
        /// CPU utilization percentage (0-100) per logical processor, averaged over the interval since the previous read.
        /// Empty if unavailable.
        /// </summary>
        IReadOnlyList<double> PerCoreCpuUtilizationPercent { get; }

        /// <summary>
        /// Total physical memory in megabytes.
        /// </summary>
        double? MemoryTotalMegabytes { get; }

        /// <summary>
        /// Used physical memory in megabytes (total minus available).
        /// </summary>
        double? MemoryUsedMegabytes { get; }

        /// <summary>
        /// Swap capacity in megabytes (Windows: paging file capacity beyond physical memory). Null if unavailable.
        /// </summary>
        double? SwapTotalMegabytes { get; }

        /// <summary>
        /// Swap in use in megabytes (Windows: committed memory beyond physical memory in use). Null if unavailable.
        /// </summary>
        double? SwapUsedMegabytes { get; }

        /// <summary>
        /// Bytes read per second across physical disks, averaged over the interval since the previous read.
        /// </summary>
        double? TotalDiskReadBytesPerSecond { get; }

        /// <summary>
        /// Bytes written per second across physical disks, averaged over the interval since the previous read.
        /// </summary>
        double? TotalDiskWriteBytesPerSecond { get; }

        /// <summary>
        /// Bytes received per second across non-loopback interfaces that are up.
        /// </summary>
        double? NetworkReceivedBytesPerSecond { get; }

        /// <summary>
        /// Bytes sent per second across non-loopback interfaces that are up.
        /// </summary>
        double? NetworkSentBytesPerSecond { get; }

        /// <summary>
        /// Per-interface throughput for non-loopback interfaces that are up.
        /// </summary>
        IReadOnlyList<NetworkInterfaceCounters> NetworkInterfaces { get; }

        /// <summary>
        /// Volumes counted toward the disk space totals.
        /// </summary>
        IReadOnlyList<VolumeInfo> Volumes { get; }

        /// <summary>
        /// One-minute load average. Linux and macOS only; null on Windows.
        /// </summary>
        double? LoadAverage1 { get; }

        /// <summary>
        /// Five-minute load average. Linux and macOS only; null on Windows.
        /// </summary>
        double? LoadAverage5 { get; }

        /// <summary>
        /// Fifteen-minute load average. Linux and macOS only; null on Windows.
        /// </summary>
        double? LoadAverage15 { get; }
    }
}
