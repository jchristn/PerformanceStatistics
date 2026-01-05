using System;
using System.IO;
using System.Text;

namespace PerformanceStatistics.Linux
{
    /// <summary>
    /// Linux system counters using /proc filesystem.
    /// </summary>
    public class LinuxSystemCounters : ISystemCounters
    {
        private (long total, long idle)? _previousCpuSample;
        private DateTime _lastCpuSampleTime;
        private DiskStats? _previousDiskStats;
        private DateTime _lastDiskSampleTime;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public LinuxSystemCounters()
        {
        }

        /// <summary>
        /// CPU utilization percentage.
        /// Requires two samples to calculate; first call initializes sampling and returns 0.
        /// </summary>
        public double? CpuUtilizationPercent
        {
            get
            {
                var current = ProcFileParser.GetCpuTimes();
                if (!current.HasValue) return null;

                if (_previousCpuSample.HasValue)
                {
                    var elapsed = DateTime.UtcNow - _lastCpuSampleTime;
                    if (elapsed.TotalMilliseconds > 50) // Minimum sample interval
                    {
                        var result = ProcFileParser.CalculateCpuPercent(
                            _previousCpuSample.Value, current.Value);
                        _previousCpuSample = current;
                        _lastCpuSampleTime = DateTime.UtcNow;
                        return Math.Round(Math.Max(0, Math.Min(100, result)), 0);
                    }
                }

                _previousCpuSample = current;
                _lastCpuSampleTime = DateTime.UtcNow;
                return 0;
            }
        }

        /// <summary>
        /// Available memory in megabytes.
        /// </summary>
        public double? MemoryFreeMegabytes
        {
            get
            {
                var kb = ProcFileParser.GetAvailableMemoryKb();
                return kb.HasValue ? Math.Round(kb.Value / 1024.0, 0) : (double?)null;
            }
        }

        /// <summary>
        /// Disk read operations per second.
        /// Requires two samples to calculate delta.
        /// </summary>
        public int? TotalDiskReadOperations
        {
            get
            {
                return GetDiskOpsPerSecond(isRead: true);
            }
        }

        /// <summary>
        /// Disk write operations per second.
        /// Requires two samples to calculate delta.
        /// </summary>
        public int? TotalDiskWriteOperations
        {
            get
            {
                return GetDiskOpsPerSecond(isRead: false);
            }
        }

        /// <summary>
        /// Disk read queue length.
        /// Approximated from I/O in progress from /proc/diskstats (divided by 2).
        /// </summary>
        public int? TotalDiskReadQueue
        {
            get
            {
                var stats = ProcFileParser.GetDiskStats();
                return stats.HasValue ? (int)Math.Max(0, stats.Value.IoInProgress / 2) : (int?)null;
            }
        }

        /// <summary>
        /// Disk write queue length.
        /// Approximated from I/O in progress from /proc/diskstats (divided by 2).
        /// </summary>
        public int? TotalDiskWriteQueue
        {
            get
            {
                var stats = ProcFileParser.GetDiskStats();
                return stats.HasValue ? (int)Math.Max(0, stats.Value.IoInProgress / 2) : (int?)null;
            }
        }

        /// <summary>
        /// Disk free percentage using DriveInfo.
        /// </summary>
        public double? TotalDiskFreePercent
        {
            get
            {
                var (free, total) = GetDiskSpace();
                if (total == 0) return null;
                return Math.Round((free / (double)total) * 100, 0);
            }
        }

        /// <summary>
        /// Disk free megabytes using DriveInfo.
        /// </summary>
        public double? TotalDiskFreeMegabytes
        {
            get
            {
                var (free, _) = GetDiskSpace();
                return Math.Round(free / (1024.0 * 1024.0), 0);
            }
        }

        /// <summary>
        /// Total disk size in megabytes.
        /// </summary>
        public double? TotalDiskSizeMegabytes
        {
            get
            {
                var (_, total) = GetDiskSpace();
                return Math.Round(total / (1024.0 * 1024.0), 0);
            }
        }

        /// <summary>
        /// Disk used megabytes.
        /// </summary>
        public double? TotalDiskUsedMegabytes
        {
            get
            {
                var (free, total) = GetDiskSpace();
                return Math.Round((total - free) / (1024.0 * 1024.0), 0);
            }
        }

        /// <summary>
        /// Produce a human-readable object.
        /// </summary>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("  CPU Utilization Percent       : " + (CpuUtilizationPercent?.ToString() ?? "N/A") + "%" + Environment.NewLine);
            sb.Append("  Memory Free (Megabytes)       : " + (MemoryFreeMegabytes?.ToString() ?? "N/A") + "MB" + Environment.NewLine);
            sb.Append("  Total Disk Read Operations    : " + (TotalDiskReadOperations?.ToString() ?? "N/A") + Environment.NewLine);
            sb.Append("  Total Disk Write Operations   : " + (TotalDiskWriteOperations?.ToString() ?? "N/A") + Environment.NewLine);
            sb.Append("  Total Disk Read Queue         : " + (TotalDiskReadQueue?.ToString() ?? "N/A") + Environment.NewLine);
            sb.Append("  Total Disk Write Queue        : " + (TotalDiskWriteQueue?.ToString() ?? "N/A") + Environment.NewLine);
            sb.Append("  Total Disk Free Percent       : " + (TotalDiskFreePercent?.ToString() ?? "N/A") + "%" + Environment.NewLine);
            sb.Append("  Total Disk Free Megabytes     : " + (TotalDiskFreeMegabytes?.ToString() ?? "N/A") + "MB" + Environment.NewLine);
            sb.Append("  Total Disk Size Megabytes     : " + (TotalDiskSizeMegabytes?.ToString() ?? "N/A") + "MB" + Environment.NewLine);
            sb.Append("  Total Disk Used Megabytes     : " + (TotalDiskUsedMegabytes?.ToString() ?? "N/A") + "MB" + Environment.NewLine);
            return sb.ToString();
        }

        private (long free, long total) GetDiskSpace()
        {
            long totalFree = 0;
            long totalSize = 0;

            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (drive.IsReady &&
                            (drive.DriveType == DriveType.Fixed ||
                             drive.DriveType == DriveType.Network))
                        {
                            // Skip pseudo-filesystems
                            string name = drive.Name.ToLowerInvariant();
                            if (name.StartsWith("/proc") ||
                                name.StartsWith("/sys") ||
                                name.StartsWith("/dev") ||
                                name.StartsWith("/run") ||
                                name.StartsWith("/snap"))
                            {
                                continue;
                            }

                            totalFree += drive.AvailableFreeSpace;
                            totalSize += drive.TotalSize;
                        }
                    }
                    catch
                    {
                        // Skip inaccessible drives
                    }
                }
            }
            catch
            {
                // Ignore errors
            }

            return (totalFree, totalSize);
        }

        private int? GetDiskOpsPerSecond(bool isRead)
        {
            var current = ProcFileParser.GetDiskStats();
            if (!current.HasValue) return null;

            if (_previousDiskStats.HasValue)
            {
                var elapsed = DateTime.UtcNow - _lastDiskSampleTime;
                if (elapsed.TotalSeconds > 0.1) // Minimum sample interval
                {
                    long delta = isRead
                        ? current.Value.ReadsCompleted - _previousDiskStats.Value.ReadsCompleted
                        : current.Value.WritesCompleted - _previousDiskStats.Value.WritesCompleted;

                    _previousDiskStats = current;
                    _lastDiskSampleTime = DateTime.UtcNow;

                    return (int)Math.Max(0, Math.Round(delta / elapsed.TotalSeconds));
                }
            }

            _previousDiskStats = current;
            _lastDiskSampleTime = DateTime.UtcNow;
            return 0;
        }
    }
}
