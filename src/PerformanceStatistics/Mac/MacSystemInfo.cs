using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using PerformanceStatistics.Internal;

namespace PerformanceStatistics.Mac
{
    /// <summary>
    /// macOS system readings. Every method returns false or null instead of throwing.
    /// </summary>
    internal static class MacSystemInfo
    {
        #region Private-Members

        private static readonly Regex _ReadOperations = new Regex("\"Operations \\(Read\\)\"=(\\d+)", RegexOptions.Compiled);
        private static readonly Regex _WriteOperations = new Regex("\"Operations \\(Write\\)\"=(\\d+)", RegexOptions.Compiled);

        #endregion

        #region Public-Methods

        /// <summary>
        /// Read cumulative CPU ticks via host_statistics(HOST_CPU_LOAD_INFO).
        /// </summary>
        /// <param name="times">Times.</param>
        /// <returns>True if read.</returns>
        public static bool TryReadCpuTimes(out CpuTimes times)
        {
            times = default(CpuTimes);
            try
            {
                int[] ticks = new int[MacNativeMethods.HOST_CPU_LOAD_INFO_COUNT];
                int count = ticks.Length;
                if (MacNativeMethods.host_statistics(MacNativeMethods.mach_host_self(), MacNativeMethods.HOST_CPU_LOAD_INFO, ticks, ref count) != 0)
                    return false;

                // user, system, idle, nice (unsigned 32-bit tick counters)
                ulong user = (uint)ticks[0];
                ulong system = (uint)ticks[1];
                ulong idle = (uint)ticks[2];
                ulong nice = (uint)ticks[3];
                times = new CpuTimes(user + system + idle + nice, idle);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Available memory = physical memory - (app memory + wired + compressed), where app memory is
        /// anonymous (internal) pages minus purgeable pages. This is how Activity Monitor computes
        /// "Memory Used", and like Linux MemAvailable it counts reclaimable file cache as available.
        /// </summary>
        /// <param name="bytes">Bytes.</param>
        /// <returns>True if read.</returns>
        public static bool TryGetAvailableMemoryBytes(out long bytes)
        {
            bytes = 0;
            try
            {
                long physical, pageSize;
                if (!MacNativeMethods.TryGetSysctl("hw.memsize", out physical)) return false;
                if (!MacNativeMethods.TryGetSysctl("hw.pagesize", out pageSize)) return false;

                int[] vm = new int[MacNativeMethods.HOST_VM_INFO64_COUNT];
                int count = vm.Length;
                if (MacNativeMethods.host_statistics64(MacNativeMethods.mach_host_self(), MacNativeMethods.HOST_VM_INFO64, vm, ref count) != 0)
                    return false;

                long internalPages = (uint)vm[MacNativeMethods.VM_INTERNAL_PAGE_COUNT];
                long purgeable = (uint)vm[MacNativeMethods.VM_PURGEABLE_COUNT];
                long wired = (uint)vm[MacNativeMethods.VM_WIRE_COUNT];
                long compressed = (uint)vm[MacNativeMethods.VM_COMPRESSOR_PAGE_COUNT];

                long used = (Math.Max(0, internalPages - purgeable) + wired + compressed) * pageSize;
                bytes = Math.Max(0, physical - used);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Sum cumulative read and write operations across block storage drivers, from the IOKit statistics
        /// that `ioreg -c IOBlockStorageDriver` reports.
        /// </summary>
        /// <param name="totals">Totals.</param>
        /// <returns>True if read.</returns>
        public static bool TryReadDiskTotals(out DiskIoTotals totals)
        {
            totals = default(DiskIoTotals);
            string output = ExecuteCommand("/usr/sbin/ioreg", "-c IOBlockStorageDriver -r -w 0");
            if (String.IsNullOrEmpty(output)) return false;

            long reads = Sum(_ReadOperations, output);
            long writes = Sum(_WriteOperations, output);
            totals = new DiskIoTotals(reads, writes);
            return true;
        }

        /// <summary>
        /// Count open file descriptors via proc_pidinfo(PROC_PIDLISTFDS).
        /// </summary>
        /// <param name="pid">Process ID.</param>
        /// <returns>Count, or null if inaccessible (another user's process) or exited.</returns>
        public static int? GetProcessFdCount(int pid)
        {
            try
            {
                int size = MacNativeMethods.proc_pidinfo(pid, MacNativeMethods.PROC_PIDLISTFDS, 0, null, 0);
                if (size <= 0) return null;

                // The size is an estimate; pad it in case descriptors are opened between the two calls.
                byte[] buffer = new byte[size + 32 * MacNativeMethods.PROC_FDINFO_SIZE];
                int used = MacNativeMethods.proc_pidinfo(pid, MacNativeMethods.PROC_PIDLISTFDS, 0, buffer, buffer.Length);
                if (used <= 0) return null;
                return used / MacNativeMethods.PROC_FDINFO_SIZE;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Physical footprint (Activity Monitor's "Memory" column) via proc_pid_rusage.
        /// </summary>
        /// <param name="pid">Process ID.</param>
        /// <returns>Bytes, or null if inaccessible (another user's process) or exited.</returns>
        public static long? GetProcessPhysicalFootprint(int pid)
        {
            try
            {
                // rusage_info_v2 is 15 uint64 values plus a 16-byte UUID; allocate generously.
                ulong[] buffer = new ulong[64];
                if (MacNativeMethods.proc_pid_rusage(pid, MacNativeMethods.RUSAGE_INFO_V2, buffer) != 0) return null;
                return (long)buffer[MacNativeMethods.RUSAGE_PHYS_FOOTPRINT];
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Private-Methods

        private static long Sum(Regex regex, string text)
        {
            long ret = 0;
            foreach (Match match in regex.Matches(text))
            {
                long value;
                if (Int64.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out value)) ret += value;
            }

            return ret;
        }

        private static string ExecuteCommand(string command, string args, int timeoutMs = 5000)
        {
            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = command,
                        Arguments = args,
                        RedirectStandardOutput = true,
                        RedirectStandardError = false,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    process.Start();

                    // Read asynchronously so a hung command cannot block past the timeout.
                    System.Threading.Tasks.Task<string> output = process.StandardOutput.ReadToEndAsync();
                    if (!process.WaitForExit(timeoutMs) || !output.Wait(timeoutMs))
                    {
                        try { process.Kill(); } catch { }
                        return null;
                    }

                    return process.ExitCode == 0 ? output.Result : null;
                }
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}
