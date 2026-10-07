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
