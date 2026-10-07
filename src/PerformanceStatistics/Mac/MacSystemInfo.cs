using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
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
        private static readonly Regex _ReadBytes = new Regex("\"Bytes \\(Read\\)\"=(\\d+)", RegexOptions.Compiled);
        private static readonly Regex _WriteBytes = new Regex("\"Bytes \\(Write\\)\"=(\\d+)", RegexOptions.Compiled);

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
            long readBytes = Sum(_ReadBytes, output);
            long writeBytes = Sum(_WriteBytes, output);
            totals = new DiskIoTotals(reads, writes, readBytes, writeBytes);
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


        /// <summary>
        /// Per-CPU load ticks via host_processor_info(PROCESSOR_CPU_LOAD_INFO).
        /// </summary>
        /// <param name="times">Times.</param>
        /// <returns>True if read.</returns>
        public static bool TryReadPerCoreCpuTimes(out CpuTimes[] times)
        {
            times = null;
            try
            {
                uint count;
                IntPtr info;
                uint infoCount;
                if (MacNativeMethods.host_processor_info(MacNativeMethods.mach_host_self(), MacNativeMethods.PROCESSOR_CPU_LOAD_INFO, out count, out info, out infoCount) != 0)
                    return false;

                try
                {
                    times = new CpuTimes[count];
                    for (int i = 0; i < count; i++)
                    {
                        // user, system, idle, nice (unsigned 32-bit)
                        ulong user = (uint)Marshal.ReadInt32(info, (i * 4 + 0) * 4);
                        ulong system = (uint)Marshal.ReadInt32(info, (i * 4 + 1) * 4);
                        ulong idle = (uint)Marshal.ReadInt32(info, (i * 4 + 2) * 4);
                        ulong nice = (uint)Marshal.ReadInt32(info, (i * 4 + 3) * 4);
                        times[i] = new CpuTimes(user + system + idle + nice, idle);
                    }
                }
                finally
                {
                    MacNativeMethods.vm_deallocate(MacNativeMethods.mach_task_self(), info, (UIntPtr)(infoCount * 4));
                }

                return times.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Swap capacity and usage via vm.swapusage (struct xsw_usage: total, avail, used).
        /// </summary>
        /// <param name="total">Capacity in bytes.</param>
        /// <param name="used">In use in bytes.</param>
        /// <returns>True if read.</returns>
        public static bool TryGetSwap(out long total, out long used)
        {
            total = 0;
            used = 0;
            byte[] usage = MacNativeMethods.GetSysctlBytes("vm.swapusage", 32);
            if (usage == null || usage.Length < 24) return false;
            total = (long)BitConverter.ToUInt64(usage, 0);
            used = (long)BitConverter.ToUInt64(usage, 16);
            return true;
        }

        /// <summary>
        /// Boot time via kern.boottime (struct timeval).
        /// </summary>
        /// <returns>Boot time (UTC) or null.</returns>
        public static DateTime? GetBootTimeUtc()
        {
            byte[] value = MacNativeMethods.GetSysctlBytes("kern.boottime", 16);
            if (value == null || value.Length < 8) return null;
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(BitConverter.ToInt64(value, 0));
        }

        /// <summary>
        /// CPU model via machdep.cpu.brand_string.
        /// </summary>
        /// <returns>Model or null.</returns>
        public static string GetCpuModel()
        {
            byte[] value = MacNativeMethods.GetSysctlBytes("machdep.cpu.brand_string", 256);
            if (value == null || value.Length == 0) return null;
            return System.Text.Encoding.ASCII.GetString(value).TrimEnd('\0').Trim();
        }

        /// <summary>
        /// Parent PID, status, and UID via proc_pidinfo(PROC_PIDT_SHORTBSDINFO), which works for every user's processes.
        /// </summary>
        /// <param name="pid">Process ID.</param>
        /// <param name="parentPid">Parent.</param>
        /// <param name="status">Status (1 idle, 2 run, 3 sleep, 4 stop, 5 zombie).</param>
        /// <param name="uid">User ID.</param>
        /// <returns>True if read.</returns>
        public static bool TryReadShortBsdInfo(int pid, out int parentPid, out uint status, out uint uid)
        {
            parentPid = 0;
            status = 0;
            uid = 0;
            try
            {
                byte[] buffer = new byte[MacNativeMethods.PROC_BSDSHORTINFO_SIZE];
                if (MacNativeMethods.proc_pidinfo(pid, MacNativeMethods.PROC_PIDT_SHORTBSDINFO, 0, buffer, buffer.Length) != buffer.Length) return false;
                parentPid = (int)BitConverter.ToUInt32(buffer, 4);
                status = BitConverter.ToUInt32(buffer, 12);
                uid = BitConverter.ToUInt32(buffer, 36);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Executable path via proc_pidpath.
        /// </summary>
        /// <param name="pid">Process ID.</param>
        /// <returns>Path or empty.</returns>
        public static string GetExecutablePath(int pid)
        {
            try
            {
                byte[] buffer = new byte[4096];
                int length = MacNativeMethods.proc_pidpath(pid, buffer, (uint)buffer.Length);
                return length > 0 ? System.Text.Encoding.UTF8.GetString(buffer, 0, length) : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Command line via sysctl KERN_PROCARGS2 (same user or elevation required).
        /// Layout: int argc, executable path, NUL padding, then argc NUL-terminated arguments, then the environment.
        /// </summary>
        /// <param name="pid">Process ID.</param>
        /// <returns>Command line or empty.</returns>
        public static string GetCommandLine(int pid)
        {
            try
            {
                long argmax;
                if (!MacNativeMethods.TryGetSysctl("kern.argmax", out argmax)) argmax = 1024 * 1024;

                byte[] buffer = new byte[argmax];
                IntPtr length = (IntPtr)buffer.Length;
                int[] mib = new int[] { MacNativeMethods.CTL_KERN, MacNativeMethods.KERN_PROCARGS2, pid };
                if (MacNativeMethods.sysctl(mib, 3, buffer, ref length, IntPtr.Zero, IntPtr.Zero) != 0) return string.Empty;

                int size = (int)length.ToInt64();
                if (size < 4) return string.Empty;
                int argc = BitConverter.ToInt32(buffer, 0);
                int offset = 4;

                // Skip the executable path and its NUL padding.
                while (offset < size && buffer[offset] != 0) offset++;
                while (offset < size && buffer[offset] == 0) offset++;

                System.Collections.Generic.List<string> args = new System.Collections.Generic.List<string>();
                while (args.Count < argc && offset < size)
                {
                    int start = offset;
                    while (offset < size && buffer[offset] != 0) offset++;
                    args.Add(System.Text.Encoding.UTF8.GetString(buffer, start, offset - start));
                    offset++;
                }

                return String.Join(" ", args);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Storage bytes read and written via proc_pid_rusage (same user or elevation required).
        /// </summary>
        /// <param name="pid">Process ID.</param>
        /// <param name="readBytes">Bytes read.</param>
        /// <param name="writeBytes">Bytes written.</param>
        /// <returns>True if read.</returns>
        public static bool TryReadProcessIo(int pid, out long readBytes, out long writeBytes)
        {
            readBytes = 0;
            writeBytes = 0;
            try
            {
                ulong[] buffer = new ulong[64];
                if (MacNativeMethods.proc_pid_rusage(pid, MacNativeMethods.RUSAGE_INFO_V2, buffer) != 0) return false;
                readBytes = (long)buffer[MacNativeMethods.RUSAGE_DISKIO_BYTESREAD];
                writeBytes = (long)buffer[MacNativeMethods.RUSAGE_DISKIO_BYTESWRITTEN];
                return true;
            }
            catch
            {
                return false;
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
