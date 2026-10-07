using System;
using System.Runtime.InteropServices;

namespace PerformanceStatistics.Mac
{
    /// <summary>
    /// macOS native interop (libSystem / libproc). Works on Intel and Apple Silicon.
    /// </summary>
    internal static class MacNativeMethods
    {
        #region Public-Members

        /// <summary>
        /// host_statistics flavor: CPU load ticks (user, system, idle, nice).
        /// </summary>
        public const int HOST_CPU_LOAD_INFO = 3;

        /// <summary>
        /// Number of natural_t values in host_cpu_load_info.
        /// </summary>
        public const int HOST_CPU_LOAD_INFO_COUNT = 4;

        /// <summary>
        /// host_statistics64 flavor: virtual memory statistics.
        /// </summary>
        public const int HOST_VM_INFO64 = 4;

        /// <summary>
        /// Size of vm_statistics64 in 32-bit words.
        /// </summary>
        public const int HOST_VM_INFO64_COUNT = 38;

        /// <summary>
        /// Word offsets of the vm_statistics64 fields used here.
        /// Layout: free, active, inactive, wire (uint32); zero_fill, reactivations, pageins, pageouts, faults,
        /// cow_faults, lookups, hits, purges (uint64); purgeable_count, speculative_count (uint32);
        /// decompressions, compressions, swapins, swapouts (uint64); compressor_page_count, throttled_count,
        /// external_page_count, internal_page_count (uint32); total_uncompressed_pages_in_compressor (uint64).
        /// </summary>
        public const int VM_WIRE_COUNT = 3;

        /// <summary>
        /// Word offset of purgeable_count.
        /// </summary>
        public const int VM_PURGEABLE_COUNT = 22;

        /// <summary>
        /// Word offset of compressor_page_count.
        /// </summary>
        public const int VM_COMPRESSOR_PAGE_COUNT = 32;

        /// <summary>
        /// Word offset of internal_page_count (anonymous memory).
        /// </summary>
        public const int VM_INTERNAL_PAGE_COUNT = 35;

        /// <summary>
        /// proc_pidinfo flavor: list of open file descriptors (8 bytes each).
        /// </summary>
        public const int PROC_PIDLISTFDS = 1;

        /// <summary>
        /// Size of struct proc_fdinfo.
        /// </summary>
        public const int PROC_FDINFO_SIZE = 8;

        /// <summary>
        /// proc_pid_rusage flavor RUSAGE_INFO_V2.
        /// </summary>
        public const int RUSAGE_INFO_V2 = 2;

        /// <summary>
        /// Offset of ri_phys_footprint in rusage_info_v2, in 64-bit words (after a 16-byte UUID and 7 uint64 fields).
        /// </summary>
        public const int RUSAGE_PHYS_FOOTPRINT = 9;

        /// <summary>
        /// Offset of ri_diskio_bytesread in rusage_info_v2, in 64-bit words.
        /// </summary>
        public const int RUSAGE_DISKIO_BYTESREAD = 18;

        /// <summary>
        /// Offset of ri_diskio_byteswritten in rusage_info_v2, in 64-bit words.
        /// </summary>
        public const int RUSAGE_DISKIO_BYTESWRITTEN = 19;

        /// <summary>
        /// proc_pidinfo flavor: struct proc_bsdshortinfo (64 bytes), readable for every user's processes.
        /// Offsets: pbsi_ppid 4, pbsi_status 12, pbsi_uid 36.
        /// </summary>
        public const int PROC_PIDT_SHORTBSDINFO = 13;

        /// <summary>
        /// Size of struct proc_bsdshortinfo.
        /// </summary>
        public const int PROC_BSDSHORTINFO_SIZE = 64;

        /// <summary>
        /// host_processor_info flavor: per-CPU load ticks (user, system, idle, nice).
        /// </summary>
        public const int PROCESSOR_CPU_LOAD_INFO = 2;

        /// <summary>
        /// sysctl MIB: CTL_KERN.
        /// </summary>
        public const int CTL_KERN = 1;

        /// <summary>
        /// sysctl MIB: KERN_PROCARGS2 (argc followed by the executable path and arguments).
        /// </summary>
        public const int KERN_PROCARGS2 = 49;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Host port for the current task.
        /// </summary>
        /// <returns>Port.</returns>
        [DllImport("libSystem.dylib")]
        public static extern uint mach_host_self();

        /// <summary>
        /// Read 32-bit host statistics.
        /// </summary>
        [DllImport("libSystem.dylib")]
        public static extern int host_statistics(uint host, int flavor, [Out] int[] info, ref int count);

        /// <summary>
        /// Read 64-bit host statistics.
        /// </summary>
        [DllImport("libSystem.dylib")]
        public static extern int host_statistics64(uint host, int flavor, [Out] int[] info, ref int count);

        /// <summary>
        /// Read a 64-bit integer sysctl by name.
        /// </summary>
        [DllImport("libSystem.dylib")]
        public static extern int sysctlbyname(string name, ref long oldp, ref IntPtr oldlenp, IntPtr newp, IntPtr newlen);

        /// <summary>
        /// Process information.
        /// </summary>
        [DllImport("libproc.dylib")]
        public static extern int proc_pidinfo(int pid, int flavor, ulong arg, [Out] byte[] buffer, int buffersize);

        /// <summary>
        /// Process resource usage.
        /// </summary>
        [DllImport("libproc.dylib")]
        public static extern int proc_pid_rusage(int pid, int flavor, [Out] ulong[] buffer);

        /// <summary>
        /// Task port for the current process.
        /// </summary>
        /// <returns>Port.</returns>
        [DllImport("libSystem.dylib")]
        public static extern uint mach_task_self();

        /// <summary>
        /// Per-processor statistics; the returned buffer must be released with vm_deallocate.
        /// </summary>
        [DllImport("libSystem.dylib")]
        public static extern int host_processor_info(uint host, int flavor, out uint processorCount, out IntPtr info, out uint infoCount);

        /// <summary>
        /// Release memory returned by Mach calls.
        /// </summary>
        [DllImport("libSystem.dylib")]
        public static extern int vm_deallocate(uint task, IntPtr address, UIntPtr size);

        /// <summary>
        /// Read a sysctl by name into a buffer.
        /// </summary>
        [DllImport("libSystem.dylib", EntryPoint = "sysctlbyname")]
        public static extern int sysctlbyname_buffer(string name, [Out] byte[] oldp, ref IntPtr oldlenp, IntPtr newp, IntPtr newlen);

        /// <summary>
        /// Read a sysctl by MIB into a buffer.
        /// </summary>
        [DllImport("libSystem.dylib")]
        public static extern int sysctl(int[] name, uint namelen, [Out] byte[] oldp, ref IntPtr oldlenp, IntPtr newp, IntPtr newlen);

        /// <summary>
        /// Executable path of a process.
        /// </summary>
        [DllImport("libproc.dylib")]
        public static extern int proc_pidpath(int pid, [Out] byte[] buffer, uint buffersize);

        /// <summary>
        /// Read a sysctl into a byte buffer.
        /// </summary>
        /// <param name="name">Name.</param>
        /// <param name="size">Maximum size.</param>
        /// <returns>Bytes read, or null.</returns>
        public static byte[] GetSysctlBytes(string name, int size)
        {
            try
            {
                byte[] buffer = new byte[size];
                IntPtr length = (IntPtr)size;
                if (sysctlbyname_buffer(name, buffer, ref length, IntPtr.Zero, IntPtr.Zero) != 0) return null;
                byte[] ret = new byte[Math.Min(size, (int)length.ToInt64())];
                Array.Copy(buffer, ret, ret.Length);
                return ret;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Read an integer sysctl (hw.memsize, hw.pagesize).
        /// </summary>
        /// <param name="name">Name.</param>
        /// <param name="value">Value.</param>
        /// <returns>True if read.</returns>
        public static bool TryGetSysctl(string name, out long value)
        {
            value = 0;
            try
            {
                IntPtr length = (IntPtr)sizeof(long);
                if (sysctlbyname(name, ref value, ref length, IntPtr.Zero, IntPtr.Zero) != 0) return false;

                // Some values (hw.pagesize on older systems) are 32-bit.
                if (length.ToInt64() == sizeof(int)) value = (int)value;
                return value > 0;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
