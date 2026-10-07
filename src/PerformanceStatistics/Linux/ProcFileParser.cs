using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using PerformanceStatistics.Internal;

namespace PerformanceStatistics.Linux
{
    /// <summary>
    /// Reads Linux kernel statistics from /proc and /sys. Every method returns false or null instead of throwing.
    /// </summary>
    internal static class ProcFileParser
    {
        #region Private-Members

        private const string ProcStat = "/proc/stat";
        private const string ProcMeminfo = "/proc/meminfo";
        private const string ProcDiskstats = "/proc/diskstats";
        private const string SysBlock = "/sys/block";

        // Virtual or layered block devices; counting them would double-count I/O already counted on the physical disk.
        private static readonly string[] _VirtualDevicePrefixes = new string[] { "loop", "ram", "zram", "dm-", "md", "nbd", "sr", "fd" };

        // Whole-disk names, used only when /sys/block is unavailable: sda, vdb, xvda, hda, nvme0n1, mmcblk0.
        private static readonly Regex _WholeDiskName = new Regex(@"^((s|h|v|xv)d[a-z]+|nvme\d+n\d+|mmcblk\d+)$", RegexOptions.Compiled);

        #endregion

        #region Public-Methods

        /// <summary>
        /// Read aggregate CPU times from the "cpu" line of /proc/stat.
        /// Total = user + nice + system + idle + iowait + irq + softirq + steal (guest time is already included in user and nice).
        /// Idle = idle + iowait.
        /// </summary>
        /// <param name="times">Times in jiffies.</param>
        /// <returns>True if read.</returns>
        public static bool TryReadCpuTimes(out CpuTimes times)
        {
            times = default(CpuTimes);
            try
            {
                foreach (string line in File.ReadLines(ProcStat))
                {
                    if (!line.StartsWith("cpu ", StringComparison.Ordinal)) continue;

                    string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 5) return false;

                    ulong total = 0;
                    ulong idle = 0;
                    int last = Math.Min(parts.Length - 1, 8);
                    for (int i = 1; i <= last; i++)
                    {
                        ulong value;
                        if (!UInt64.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out value)) return false;
                        total += value;
                        if (i == 4 || i == 5) idle += value;
                    }

                    times = new CpuTimes(total, idle);
                    return true;
                }
            }
            catch
            {
                // /proc unavailable or unreadable.
            }

            return false;
        }

        /// <summary>
        /// Read available memory from /proc/meminfo: MemAvailable (kernel 3.14+), else MemFree + Buffers + Cached.
        /// </summary>
        /// <param name="bytes">Bytes.</param>
        /// <returns>True if read.</returns>
        public static bool TryGetAvailableMemoryBytes(out long bytes)
        {
            bytes = 0;
            Dictionary<string, long> kb = ReadMeminfo();
            if (kb == null) return false;

            long value;
            if (kb.TryGetValue("MemAvailable", out value))
            {
                bytes = value * 1024;
                return true;
            }

            if (!kb.ContainsKey("MemFree")) return false;

            long buffers, cached;
            kb.TryGetValue("Buffers", out buffers);
            kb.TryGetValue("Cached", out cached);
            bytes = (kb["MemFree"] + buffers + cached) * 1024;
            return true;
        }

        /// <summary>
        /// Sum completed reads and writes across physical whole disks from /proc/diskstats.
        /// </summary>
        /// <param name="totals">Totals.</param>
        /// <returns>True if read.</returns>
        public static bool TryReadDiskTotals(out DiskIoTotals totals)
        {
            totals = default(DiskIoTotals);
            try
            {
                HashSet<string> disks = GetPhysicalDisks();
                long reads = 0;
                long writes = 0;

                foreach (string line in File.ReadLines(ProcDiskstats))
                {
                    // major minor name reads-completed reads-merged sectors-read ms-reading writes-completed ...
                    string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 8) continue;

                    string name = parts[2];
                    if (disks != null ? !disks.Contains(name) : !_WholeDiskName.IsMatch(name)) continue;

                    long value;
                    if (Int64.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out value)) reads += value;
                    if (Int64.TryParse(parts[7], NumberStyles.None, CultureInfo.InvariantCulture, out value)) writes += value;
                }

                totals = new DiskIoTotals(reads, writes);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Sum in-flight read and write requests across physical whole disks from /sys/block/&lt;disk&gt;/inflight.
        /// </summary>
        /// <param name="reads">In-flight reads.</param>
        /// <param name="writes">In-flight writes.</param>
        /// <returns>True if at least one disk was read.</returns>
        public static bool TryReadInflight(out long reads, out long writes)
        {
            reads = 0;
            writes = 0;
            HashSet<string> disks = GetPhysicalDisks();
            if (disks == null) return false;

            bool any = false;
            foreach (string disk in disks)
            {
                try
                {
                    string[] parts = File.ReadAllText(Path.Combine(SysBlock, disk, "inflight"))
                        .Split(new char[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;

                    long r, w;
                    if (!Int64.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out r)) continue;
                    if (!Int64.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out w)) continue;
                    reads += r;
                    writes += w;
                    any = true;
                }
                catch
                {
                    // Disk removed or file unreadable.
                }
            }

            return any;
        }

        /// <summary>
        /// Count open file descriptors of a process from /proc/&lt;pid&gt;/fd.
        /// </summary>
        /// <param name="pid">Process ID.</param>
        /// <returns>Count, or null if unreadable (exited, or another user's process without privileges).</returns>
        public static int? GetProcessFdCount(int pid)
        {
            try
            {
                return Directory.GetFileSystemEntries("/proc/" + pid.ToString(CultureInfo.InvariantCulture) + "/fd").Length;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Private-Methods

        private static Dictionary<string, long> ReadMeminfo()
        {
            try
            {
                Dictionary<string, long> ret = new Dictionary<string, long>(StringComparer.Ordinal);
                foreach (string line in File.ReadLines(ProcMeminfo))
                {
                    // "MemAvailable:   12345678 kB"
                    int colon = line.IndexOf(':');
                    if (colon < 1) continue;

                    string[] value = line.Substring(colon + 1).Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    long kb;
                    if (value.Length > 0 && Int64.TryParse(value[0], NumberStyles.None, CultureInfo.InvariantCulture, out kb))
                        ret[line.Substring(0, colon).Trim()] = kb;
                }

                return ret;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Physical whole disks: the entries of /sys/block (which lists whole disks, never partitions)
        /// minus virtual and layered devices. Null if /sys/block is unavailable.
        /// </summary>
        private static HashSet<string> GetPhysicalDisks()
        {
            try
            {
                if (!Directory.Exists(SysBlock)) return null;

                HashSet<string> ret = new HashSet<string>(StringComparer.Ordinal);
                foreach (string entry in Directory.GetFileSystemEntries(SysBlock))
                {
                    string name = Path.GetFileName(entry);
                    bool isVirtual = false;
                    foreach (string prefix in _VirtualDevicePrefixes)
                    {
                        if (name.StartsWith(prefix, StringComparison.Ordinal))
                        {
                            isVirtual = true;
                            break;
                        }
                    }

                    if (!isVirtual) ret.Add(name);
                }

                return ret;
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}
