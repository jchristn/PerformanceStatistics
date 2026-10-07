using System;
using System.Runtime.InteropServices;

namespace PerformanceStatistics.Windows
{
    /// <summary>
    /// Windows native interop (kernel32).
    /// </summary>
    internal static class WindowsNativeMethods
    {
        #region Public-Members

        /// <summary>
        /// MEMORYSTATUSEX.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORYSTATUSEX
        {
            /// <summary>Structure size; must be set before the call.</summary>
            public uint dwLength;
            /// <summary>Percent of physical memory in use.</summary>
            public uint dwMemoryLoad;
            /// <summary>Total physical memory in bytes.</summary>
            public ulong ullTotalPhys;
            /// <summary>Available physical memory in bytes.</summary>
            public ulong ullAvailPhys;
            /// <summary>Commit limit in bytes.</summary>
            public ulong ullTotalPageFile;
            /// <summary>Available commit in bytes.</summary>
            public ulong ullAvailPageFile;
            /// <summary>Total user-mode virtual address space in bytes.</summary>
            public ulong ullTotalVirtual;
            /// <summary>Available user-mode virtual address space in bytes.</summary>
            public ulong ullAvailVirtual;
            /// <summary>Reserved.</summary>
            public ulong ullAvailExtendedVirtual;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// System-wide idle, kernel (including idle), and user time, in 100ns units.
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetSystemTimes(out long idleTime, out long kernelTime, out long userTime);

        /// <summary>
        /// Physical and virtual memory status.
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);

        #endregion
    }
}
