using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PerformanceStatistics.Mac
{
    /// <summary>
    /// Utility class for macOS system information retrieval.
    /// </summary>
    internal static class MacSystemInfo
    {
        /// <summary>
        /// Executes a command and returns stdout.
        /// </summary>
        private static string ExecuteCommand(string command, string args, int timeoutMs = 5000)
        {
            try
            {
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = command,
                        Arguments = args,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    process.Start();

                    string output = process.StandardOutput.ReadToEnd();

                    if (!process.WaitForExit(timeoutMs))
                    {
                        try { process.Kill(); } catch { }
                        return null;
                    }

                    return output;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets CPU utilization using top command.
        /// Returns percentage of CPU in use (100 - idle).
        /// </summary>
        public static double? GetCpuUtilization()
        {
            try
            {
                // top -l 1 -n 0 -s 0 outputs CPU usage
                // Look for line like: "CPU usage: 5.55% user, 8.33% sys, 86.11% idle"
                var output = ExecuteCommand("/usr/bin/top", "-l 1 -n 0 -s 0");
                if (string.IsNullOrEmpty(output)) return null;

                foreach (string line in output.Split('\n'))
                {
                    if (line.Contains("CPU usage:"))
                    {
                        // Extract idle percentage
                        var match = Regex.Match(line, @"(\d+\.?\d*)\s*%\s*idle");
                        if (match.Success && double.TryParse(match.Groups[1].Value, out double idle))
                        {
                            return Math.Max(0, 100 - idle);
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors
            }

            return null;
        }

        /// <summary>
        /// Gets available memory using vm_stat.
        /// Returns available memory in bytes.
        /// </summary>
        public static long? GetAvailableMemoryBytes()
        {
            try
            {
                var vmstat = ExecuteCommand("/usr/bin/vm_stat", "");
                if (string.IsNullOrEmpty(vmstat)) return null;

                long pageSize = GetPageSize();
                long pagesFree = 0;
                long pagesSpeculative = 0;
                long pagesPurgeable = 0;

                foreach (string line in vmstat.Split('\n'))
                {
                    // Lines look like: "Pages free:                              123456."
                    if (line.StartsWith("Pages free:"))
                    {
                        pagesFree = ParseVmStatValue(line);
                    }
                    else if (line.StartsWith("Pages speculative:"))
                    {
                        pagesSpeculative = ParseVmStatValue(line);
                    }
                    else if (line.StartsWith("Pages purgeable:"))
                    {
                        pagesPurgeable = ParseVmStatValue(line);
                    }
                }

                // Available memory = free + speculative + purgeable
                long availablePages = pagesFree + pagesSpeculative + pagesPurgeable;
                return availablePages * pageSize;
            }
            catch
            {
                return null;
            }
        }

        private static long ParseVmStatValue(string line)
        {
            // Format: "Pages free:                              123456."
            var match = Regex.Match(line, @":\s*(\d+)");
            if (match.Success && long.TryParse(match.Groups[1].Value, out long value))
            {
                return value;
            }
            return 0;
        }

        /// <summary>
        /// Gets memory page size.
        /// </summary>
        public static long GetPageSize()
        {
            try
            {
                var output = ExecuteCommand("/usr/sbin/sysctl", "-n hw.pagesize");
                if (!string.IsNullOrEmpty(output) && long.TryParse(output.Trim(), out long size))
                {
                    return size;
                }
            }
            catch
            {
                // Ignore errors
            }

            // Default page sizes:
            // Intel Macs: 4096 (4KB)
            // Apple Silicon: 16384 (16KB)
            return 4096;
        }

        /// <summary>
        /// Gets disk I/O statistics using iostat.
        /// Returns combined transfer operations per second.
        /// </summary>
        public static MacDiskStats? GetDiskStats()
        {
            try
            {
                // iostat -d outputs disk transfer info
                // Format varies, but we can extract transfers per second
                var output = ExecuteCommand("/usr/sbin/iostat", "-d -c 2");
                if (string.IsNullOrEmpty(output)) return null;

                var stats = new MacDiskStats();
                string[] lines = output.Split('\n');

                // Skip header lines, parse data lines
                bool inData = false;
                int dataLineCount = 0;

                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    // Skip header line containing "KB/t"
                    if (trimmed.Contains("KB/t"))
                    {
                        inData = true;
                        continue;
                    }

                    if (inData)
                    {
                        dataLineCount++;
                        // Use second sample (more accurate than first)
                        if (dataLineCount >= 2)
                        {
                            // Format: KB/t tps MB/s [repeated per disk]
                            string[] parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            // Sum up tps from all disks (every 3rd value starting from index 1)
                            for (int i = 1; i < parts.Length; i += 3)
                            {
                                if (double.TryParse(parts[i], out double tps))
                                {
                                    stats.TransfersPerSecond += tps;
                                }
                            }
                            break;
                        }
                    }
                }

                return stats;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets process CPU time using the Process class.
        /// </summary>
        public static (TimeSpan userTime, TimeSpan sysTime)? GetProcessCpuTime(int pid)
        {
            try
            {
                var process = Process.GetProcessById(pid);
                return (process.UserProcessorTime, process.PrivilegedProcessorTime);
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Disk statistics from iostat.
    /// </summary>
    internal struct MacDiskStats
    {
        public double TransfersPerSecond;
    }
}
