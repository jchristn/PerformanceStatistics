using System;

namespace PerformanceStatistics
{
    /// <summary>
    /// Interface for process-level performance counters.
    /// </summary>
    public interface IProcessCounters : IDisposable
    {
        /// <summary>
        /// Process ID.
        /// </summary>
        int? ProcessId { get; }

        /// <summary>
        /// Process name.
        /// </summary>
        string ProcessName { get; }

        /// <summary>
        /// Main window title.
        /// Typically empty on non-Windows platforms as there is no window concept.
        /// </summary>
        string ProcessTitle { get; }

        /// <summary>
        /// Machine name.
        /// </summary>
        string MachineName { get; }

        /// <summary>
        /// CPU utilization percentage for this process.
        /// Returns null if the metric is unavailable on this platform.
        /// Note: May return 0 on first call as CPU calculation requires two samples.
        /// </summary>
        double? CpuUtilizationPercent { get; }

        /// <summary>
        /// Number of handles.
        /// On Windows, returns the actual handle count.
        /// On Linux, returns the file descriptor count from /proc/[pid]/fd.
        /// On macOS, returns 0 as this metric is not readily available.
        /// </summary>
        int? HandleCount { get; }

        /// <summary>
        /// Number of threads.
        /// </summary>
        int? ThreadCount { get; }

        /// <summary>
        /// Non-paged system memory in bytes.
        /// Returns null on non-Windows platforms (Windows-specific concept).
        /// </summary>
        long? NonPagedSystemMemory { get; }

        /// <summary>
        /// Paged system memory in bytes.
        /// Returns null on non-Windows platforms (Windows-specific concept).
        /// </summary>
        long? PagedSystemMemory { get; }

        /// <summary>
        /// Private memory in bytes.
        /// </summary>
        long? PrivateMemory { get; }

        /// <summary>
        /// Virtual memory in bytes.
        /// </summary>
        long? VirtualMemory { get; }

        /// <summary>
        /// Working set memory in bytes.
        /// </summary>
        long? WorkingSetMemory { get; }

        /// <summary>
        /// Peak paged memory in bytes.
        /// Returns null on non-Windows platforms (Windows-specific tracking).
        /// </summary>
        long? PeakPagedSystemMemory { get; }

        /// <summary>
        /// Peak virtual memory in bytes.
        /// </summary>
        long? PeakVirtualSystemMemory { get; }

        /// <summary>
        /// Peak working set memory in bytes.
        /// </summary>
        long? PeakWorkingSetMemory { get; }
    }
}
