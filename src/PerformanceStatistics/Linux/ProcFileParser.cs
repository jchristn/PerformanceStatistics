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

        // PERFSTATS_HOST_ROOT (for example "/host") lets an agent in a container read the host's /proc and /sys
        // when they are mounted at <root>/proc and <root>/sys.
        private static readonly string _HostRoot = (Environment.GetEnvironmentVariable("PERFSTATS_HOST_ROOT") ?? string.Empty).TrimEnd('/');
        private static readonly string ProcRoot = _HostRoot + "/proc";
        private static readonly string ProcStat = ProcRoot + "/stat";
        private static readonly string ProcMeminfo = ProcRoot + "/meminfo";
        private static readonly string ProcDiskstats = ProcRoot + "/diskstats";
        private static readonly string ProcLoadavg = ProcRoot + "/loadavg";
        private static readonly string ProcCpuinfo = ProcRoot + "/cpuinfo";
        private static readonly string SysBlock = _HostRoot + "/sys/block";
        private const int SectorBytes = 512;

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
                    if (line.StartsWith("cpu ", StringComparison.Ordinal)) return TryParseCpuLine(line, out times);
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
                long readBytes = 0;
                long writeBytes = 0;

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
                    if (Int64.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out value)) readBytes += value * SectorBytes;
                    if (parts.Length > 9 && Int64.TryParse(parts[9], NumberStyles.None, CultureInfo.InvariantCulture, out value)) writeBytes += value * SectorBytes;
                }

                totals = new DiskIoTotals(reads, writes, readBytes, writeBytes);
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
                return Directory.GetFileSystemEntries(ProcPath(pid, "fd")).Length;
            }
            catch
            {
                return null;
            }
        }


        /// <summary>
        /// Read per-processor CPU times from the "cpuN" lines of /proc/stat.
        /// </summary>
        /// <param name="times">Times, ordered by processor number.</param>
        /// <returns>True if read.</returns>
        public static bool TryReadPerCoreCpuTimes(out CpuTimes[] times)
        {
            times = null;
            try
            {
                List<CpuTimes> ret = new List<CpuTimes>();
                foreach (string line in File.ReadLines(ProcStat))
                {
                    if (!line.StartsWith("cpu", StringComparison.Ordinal) || line.Length < 4 || !Char.IsDigit(line[3])) continue;
                    CpuTimes t;
                    if (TryParseCpuLine(line, out t)) ret.Add(t);
                }

                times = ret.ToArray();
                return times.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Read MemTotal from /proc/meminfo.
        /// </summary>
        /// <param name="bytes">Bytes.</param>
        /// <returns>True if read.</returns>
        public static bool TryGetTotalMemoryBytes(out long bytes)
        {
            bytes = 0;
            Dictionary<string, long> kb = ReadMeminfo();
            long value;
            if (kb == null || !kb.TryGetValue("MemTotal", out value)) return false;
            bytes = value * 1024;
            return true;
        }

        /// <summary>
        /// Read SwapTotal and SwapTotal - SwapFree from /proc/meminfo.
        /// </summary>
        /// <param name="total">Capacity in bytes.</param>
        /// <param name="used">In use in bytes.</param>
        /// <returns>True if read.</returns>
        public static bool TryGetSwap(out long total, out long used)
        {
            total = 0;
            used = 0;
            Dictionary<string, long> kb = ReadMeminfo();
            long swapTotal, swapFree;
            if (kb == null || !kb.TryGetValue("SwapTotal", out swapTotal) || !kb.TryGetValue("SwapFree", out swapFree)) return false;
            total = swapTotal * 1024;
            used = Math.Max(0, swapTotal - swapFree) * 1024;
            return true;
        }

        /// <summary>
        /// Read the 1, 5, and 15 minute load averages from /proc/loadavg.
        /// </summary>
        /// <param name="loads">Loads.</param>
        /// <returns>True if read.</returns>
        public static bool TryGetLoadAverages(out double[] loads)
        {
            loads = null;
            try
            {
                string[] parts = File.ReadAllText(ProcLoadavg).Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) return false;
                loads = new double[3];
                for (int i = 0; i < 3; i++)
                {
                    if (!Double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out loads[i])) return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Boot time from the "btime" line of /proc/stat.
        /// </summary>
        /// <returns>Boot time (UTC) or null.</returns>
        public static DateTime? GetBootTimeUtc()
        {
            try
            {
                foreach (string line in File.ReadLines(ProcStat))
                {
                    if (!line.StartsWith("btime ", StringComparison.Ordinal)) continue;
                    long seconds;
                    if (Int64.TryParse(line.Substring(6).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out seconds))
                        return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds);
                }
            }
            catch
            {
            }

            return null;
        }

        /// <summary>
        /// CPU model from /proc/cpuinfo ("model name", or "Hardware"/"Model" on ARM).
        /// </summary>
        /// <returns>Model or null.</returns>
        public static string GetCpuModel()
        {
            try
            {
                string fallback = null;
                foreach (string line in File.ReadLines(ProcCpuinfo))
                {
                    int colon = line.IndexOf(':');
                    if (colon < 1) continue;
                    string key = line.Substring(0, colon).Trim();
                    string value = line.Substring(colon + 1).Trim();
                    if (value.Length == 0) continue;
                    if (String.Equals(key, "model name", StringComparison.Ordinal)) return value;
                    if (fallback == null && (String.Equals(key, "Hardware", StringComparison.Ordinal) || String.Equals(key, "Model", StringComparison.Ordinal))) fallback = value;
                }

                return fallback;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Read state and parent PID from /proc/&lt;pid&gt;/stat.
        /// </summary>
        /// <param name="pid">Process ID.</param>
        /// <param name="state">State character (R, S, D, T, t, Z, I, ...).</param>
        /// <param name="parentPid">Parent process ID.</param>
        /// <returns>True if read.</returns>
        public static bool TryReadProcessStat(int pid, out char state, out int parentPid)
        {
            state = '?';
            parentPid = 0;
            try
            {
                // The command name (field 2) can contain spaces and parentheses; parse after the last ')'.
                string content = File.ReadAllText(ProcPath(pid, "stat"));
                int lastParen = content.LastIndexOf(')');
                if (lastParen < 0) return false;
                string[] parts = content.Substring(lastParen + 1).Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || parts[0].Length < 1) return false;
                state = parts[0][0];
                return Int32.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out parentPid);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Executable path from the /proc/&lt;pid&gt;/exe link.
        /// </summary>
        /// <param name="pid">Process ID.</param>
        /// <returns>Path or empty.</returns>
        public static string GetExecutablePath(int pid)
        {
            string target = UnixNativeMethods.ReadLink(ProcPath(pid, "exe"));
            if (target == null) return string.Empty;
            const string deleted = " (deleted)";
            return target.EndsWith(deleted, StringComparison.Ordinal) ? target.Substring(0, target.Length - deleted.Length) : target;
        }

        /// <summary>
        /// Command line from /proc/&lt;pid&gt;/cmdline (NUL-separated arguments joined by spaces).
        /// </summary>
        /// <param name="pid">Process ID.</param>
        /// <returns>Command line or empty.</returns>
        public static string GetCommandLine(int pid)
        {
            try
            {
                byte[] raw = File.ReadAllBytes(ProcPath(pid, "cmdline"));
                return System.Text.Encoding.UTF8.GetString(raw).TrimEnd('\0').Replace('\0', ' ');
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Owning user from the real UID in /proc/&lt;pid&gt;/status.
        /// </summary>
        /// <param name="pid">Process ID.</param>
        /// <returns>User name or empty.</returns>
        public static string GetUserName(int pid)
        {
            try
            {
                foreach (string line in File.ReadLines(ProcPath(pid, "status")))
                {
                    if (!line.StartsWith("Uid:", StringComparison.Ordinal)) continue;
                    string[] parts = line.Substring(4).Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    uint uid;
                    if (parts.Length > 0 && UInt32.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out uid))
                        return UnixNativeMethods.GetUserName(uid);
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        /// <summary>
        /// Storage bytes read and written from /proc/&lt;pid&gt;/io (read_bytes, write_bytes).
        /// Readable for the caller's own processes, or all processes with elevation.
        /// </summary>
        /// <param name="pid">Process ID.</param>
        /// <param name="readBytes">Bytes read.</param>
        /// <param name="writeBytes">Bytes written.</param>
        /// <returns>True if read.</returns>
        public static bool TryReadProcessIo(int pid, out long readBytes, out long writeBytes)
        {
            readBytes = 0;
            writeBytes = 0;
            bool gotRead = false;
            bool gotWrite = false;
            try
            {
                foreach (string line in File.ReadLines(ProcPath(pid, "io")))
                {
                    if (line.StartsWith("read_bytes:", StringComparison.Ordinal))
                        gotRead = Int64.TryParse(line.Substring(11).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out readBytes);
                    else if (line.StartsWith("write_bytes:", StringComparison.Ordinal))
                        gotWrite = Int64.TryParse(line.Substring(12).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out writeBytes);
                }
            }
            catch
            {
                return false;
            }

            return gotRead && gotWrite;
        }

        #endregion

        #region Private-Methods

        private static string ProcPath(int pid, string entry)
        {
            return ProcRoot + "/" + pid.ToString(CultureInfo.InvariantCulture) + "/" + entry;
        }

        private static bool TryParseCpuLine(string line, out CpuTimes times)
        {
            times = default(CpuTimes);
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
