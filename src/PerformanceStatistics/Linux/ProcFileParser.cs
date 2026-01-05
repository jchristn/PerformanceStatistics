using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PerformanceStatistics.Linux
{
    /// <summary>
    /// Utility class for parsing Linux /proc filesystem.
    /// </summary>
    internal static class ProcFileParser
    {
        /// <summary>
        /// Reads and parses /proc/stat to get CPU statistics.
        /// Returns (total jiffies, idle jiffies) tuple.
        /// </summary>
        public static (long total, long idle)? GetCpuTimes()
        {
            try
            {
                string content = File.ReadAllText("/proc/stat");
                string[] lines = content.Split('\n');

                foreach (string line in lines)
                {
                    if (line.StartsWith("cpu "))
                    {
                        // Format: cpu user nice system idle iowait irq softirq steal guest guest_nice
                        string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 5)
                        {
                            long user = long.Parse(parts[1]);
                            long nice = long.Parse(parts[2]);
                            long system = long.Parse(parts[3]);
                            long idle = long.Parse(parts[4]);
                            long iowait = parts.Length > 5 ? long.Parse(parts[5]) : 0;
                            long irq = parts.Length > 6 ? long.Parse(parts[6]) : 0;
                            long softirq = parts.Length > 7 ? long.Parse(parts[7]) : 0;
                            long steal = parts.Length > 8 ? long.Parse(parts[8]) : 0;

                            long total = user + nice + system + idle + iowait + irq + softirq + steal;
                            long idleTotal = idle + iowait;

                            return (total, idleTotal);
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors - file may not exist or be readable
            }

            return null;
        }

        /// <summary>
        /// Calculates CPU utilization percentage from two samples.
        /// </summary>
        public static double CalculateCpuPercent((long total, long idle) prev, (long total, long idle) curr)
        {
            long deltaTotal = curr.total - prev.total;
            long deltaIdle = curr.idle - prev.idle;

            if (deltaTotal == 0) return 0;

            return 100.0 * (deltaTotal - deltaIdle) / deltaTotal;
        }

        /// <summary>
        /// Reads /proc/meminfo and returns available memory in KB.
        /// Uses MemAvailable if present (kernel 3.14+), otherwise MemFree + Buffers + Cached.
        /// </summary>
        public static long? GetAvailableMemoryKb()
        {
            try
            {
                string content = File.ReadAllText("/proc/meminfo");
                var memInfo = new Dictionary<string, long>();

                foreach (string line in content.Split('\n'))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    string[] parts = line.Split(new[] { ':' }, 2);
                    if (parts.Length == 2)
                    {
                        string key = parts[0].Trim();
                        string valueStr = parts[1].Trim().Split(' ')[0];
                        if (long.TryParse(valueStr, out long value))
                        {
                            memInfo[key] = value;
                        }
                    }
                }

                // Prefer MemAvailable (kernel 3.14+)
                if (memInfo.TryGetValue("MemAvailable", out long memAvailable))
                {
                    return memAvailable;
                }

                // Fallback: MemFree + Buffers + Cached
                long memFree = memInfo.TryGetValue("MemFree", out long mf) ? mf : 0;
                long buffers = memInfo.TryGetValue("Buffers", out long b) ? b : 0;
                long cached = memInfo.TryGetValue("Cached", out long c) ? c : 0;

                return memFree + buffers + cached;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Parses /proc/diskstats and returns aggregated I/O stats.
        /// </summary>
        public static DiskStats? GetDiskStats()
        {
            try
            {
                string content = File.ReadAllText("/proc/diskstats");
                var stats = new DiskStats();

                foreach (string line in content.Split('\n'))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 14) continue;

                    string deviceName = parts[2];

                    // Skip loop devices, ram devices, and device mapper partitions
                    if (deviceName.StartsWith("loop") ||
                        deviceName.StartsWith("ram") ||
                        deviceName.StartsWith("dm-") ||
                        char.IsDigit(deviceName[deviceName.Length - 1]) && !deviceName.StartsWith("nvme"))
                    {
                        // Skip partitions (e.g., sda1, sda2) but keep whole disks (e.g., sda)
                        // Exception: nvme devices like nvme0n1 are whole disks
                        if (!deviceName.StartsWith("nvme") && char.IsDigit(deviceName[deviceName.Length - 1]))
                            continue;
                    }

                    // Field indices (0-based after split):
                    // 3: reads completed
                    // 7: writes completed
                    // 6: time spent reading (ms)
                    // 10: time spent writing (ms)
                    // 11: I/Os currently in progress

                    if (long.TryParse(parts[3], out long reads))
                        stats.ReadsCompleted += reads;
                    if (long.TryParse(parts[7], out long writes))
                        stats.WritesCompleted += writes;
                    if (long.TryParse(parts[6], out long readTime))
                        stats.ReadTimeMs += readTime;
                    if (long.TryParse(parts[10], out long writeTime))
                        stats.WriteTimeMs += writeTime;
                    if (parts.Length > 11 && long.TryParse(parts[11], out long ioInProgress))
                        stats.IoInProgress += ioInProgress;
                }

                return stats;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Reads /proc/[pid]/stat for process CPU times.
        /// Returns (utime, stime) in clock ticks.
        /// </summary>
        public static (long utime, long stime)? GetProcessCpuTimes(int pid)
        {
            try
            {
                string content = File.ReadAllText($"/proc/{pid}/stat");

                // Format is complex - the command name (field 2) can contain spaces and parentheses
                // Find the last ')' to locate end of command name
                int lastParen = content.LastIndexOf(')');
                if (lastParen < 0) return null;

                string afterComm = content.Substring(lastParen + 1).Trim();
                string[] parts = afterComm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                // After the command name:
                // Index 0: state
                // Index 11: utime (field 14 in full stat, 0-indexed from here)
                // Index 12: stime (field 15 in full stat)
                if (parts.Length > 12)
                {
                    if (long.TryParse(parts[11], out long utime) &&
                        long.TryParse(parts[12], out long stime))
                    {
                        return (utime, stime);
                    }
                }
            }
            catch
            {
                // Process may have exited or permission denied
            }

            return null;
        }

        /// <summary>
        /// Counts file descriptors for a process via /proc/[pid]/fd.
        /// </summary>
        public static int? GetProcessFdCount(int pid)
        {
            try
            {
                string fdPath = $"/proc/{pid}/fd";
                if (Directory.Exists(fdPath))
                {
                    return Directory.GetFiles(fdPath).Length;
                }
            }
            catch
            {
                // Permission denied or process exited
            }

            return null;
        }

        /// <summary>
        /// Gets clock ticks per second (USER_HZ, typically 100 on Linux).
        /// </summary>
        public static long GetClockTicksPerSecond()
        {
            // On most Linux systems this is 100 (sysconf(_SC_CLK_TCK))
            // This is a compile-time constant in the kernel, typically 100
            return 100;
        }
    }

    /// <summary>
    /// Aggregated disk statistics from /proc/diskstats.
    /// </summary>
    internal struct DiskStats
    {
        public long ReadsCompleted;
        public long WritesCompleted;
        public long ReadTimeMs;
        public long WriteTimeMs;
        public long IoInProgress;
    }
}
