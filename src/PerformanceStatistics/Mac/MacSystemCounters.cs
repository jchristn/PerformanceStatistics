using System;
using System.IO;
using System.Text;

namespace PerformanceStatistics.Mac
{
    /// <summary>
    /// macOS system counters.
    /// </summary>
    public class MacSystemCounters : ISystemCounters
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        public MacSystemCounters()
        {
        }

        /// <summary>
        /// CPU utilization percentage.
        /// </summary>
        public double? CpuUtilizationPercent
        {
            get
            {
                var cpu = MacSystemInfo.GetCpuUtilization();
                return cpu.HasValue ? Math.Round(cpu.Value, 0) : (double?)null;
            }
        }

        /// <summary>
        /// Available memory in megabytes.
        /// </summary>
        public double? MemoryFreeMegabytes
        {
            get
            {
                var bytes = MacSystemInfo.GetAvailableMemoryBytes();
                return bytes.HasValue ? Math.Round(bytes.Value / (1024.0 * 1024.0), 0) : (double?)null;
            }
        }

        /// <summary>
        /// Disk read operations per second.
        /// Note: macOS iostat provides combined transfers; this returns half as approximation.
        /// </summary>
        public int? TotalDiskReadOperations
        {
            get
            {
                var stats = MacSystemInfo.GetDiskStats();
                return stats.HasValue ? (int)Math.Round(stats.Value.TransfersPerSecond / 2) : (int?)null;
            }
        }

        /// <summary>
        /// Disk write operations per second.
        /// Note: macOS iostat provides combined transfers; this returns half as approximation.
        /// </summary>
        public int? TotalDiskWriteOperations
        {
            get
            {
                var stats = MacSystemInfo.GetDiskStats();
                return stats.HasValue ? (int)Math.Round(stats.Value.TransfersPerSecond / 2) : (int?)null;
            }
        }

        /// <summary>
        /// Disk read queue length.
        /// Not readily available on macOS; returns null.
        /// </summary>
        public int? TotalDiskReadQueue => null;

        /// <summary>
        /// Disk write queue length.
        /// Not readily available on macOS; returns null.
        /// </summary>
        public int? TotalDiskWriteQueue => null;

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
                        if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                        {
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
    }
}
