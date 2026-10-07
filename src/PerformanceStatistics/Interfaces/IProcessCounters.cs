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
        /// CPU utilization of this process as a percentage (0-100) of total machine capacity (all cores).
        /// Returns null if the metric is unavailable on this platform.
        /// Averaged over the interval since the previous read, or since the counters were created.
        /// </summary>
        double? CpuUtilizationPercent { get; }

        /// <summary>
        /// Number of handles.
        /// On Windows, returns the actual handle count.
        /// On Linux and macOS, returns the open file descriptor count (null for other users' processes).
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

        /// <summary>
        /// Parent process ID, or null if unavailable.
        /// </summary>
        int? ParentProcessId { get; }

        /// <summary>
        /// Full path of the process executable, or empty if unavailable (exited, or not permitted).
        /// </summary>
        string ExecutablePath { get; }

        /// <summary>
        /// Command line (arguments joined by spaces), or empty if unavailable.
        /// On macOS this requires the same user or elevation.
        /// </summary>
        string CommandLine { get; }

        /// <summary>
        /// Owning user name (Windows: DOMAIN\user), or empty if unavailable.
        /// </summary>
        string UserName { get; }

        /// <summary>
        /// Process start time (UTC), or null if unavailable.
        /// </summary>
        DateTime? StartTimeUtc { get; }

        /// <summary>
        /// Scheduling state.
        /// </summary>
        ProcessStateEnum State { get; }

        /// <summary>
        /// Priority on the cross-platform scale, or null if unavailable.
        /// </summary>
        ProcessPriorityEnum? Priority { get; }

        /// <summary>
        /// Total CPU time consumed since the process started, or null if unavailable.
        /// </summary>
        TimeSpan? TotalProcessorTime { get; }

        /// <summary>
        /// Bytes read from storage per second, averaged over the interval since the previous read.
        /// Null if unavailable (Linux: other users' processes without elevation).
        /// </summary>
        double? DiskReadBytesPerSecond { get; }

        /// <summary>
        /// Bytes written to storage per second, averaged over the interval since the previous read.
        /// Null if unavailable.
        /// </summary>
        double? DiskWriteBytesPerSecond { get; }

        /// <summary>
        /// Total bytes read since the process started, or null if unavailable.
        /// Windows counts all file I/O; Linux and macOS count storage I/O.
        /// </summary>
        long? DiskReadBytesTotal { get; }

        /// <summary>
        /// Total bytes written since the process started, or null if unavailable.
        /// </summary>
        long? DiskWriteBytesTotal { get; }
    }
}
