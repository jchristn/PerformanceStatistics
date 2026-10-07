using System;
using System.Runtime.InteropServices;
using System.Text;
using PerformanceStatistics.Internal;

namespace PerformanceStatistics.Windows
{
    /// <summary>
    /// Windows native interop (kernel32, advapi32, ntdll).
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

        /// <summary>
        /// IO_COUNTERS.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct IO_COUNTERS
        {
            /// <summary>Read operations.</summary>
            public ulong ReadOperationCount;
            /// <summary>Write operations.</summary>
            public ulong WriteOperationCount;
            /// <summary>Other operations.</summary>
            public ulong OtherOperationCount;
            /// <summary>Bytes read.</summary>
            public ulong ReadTransferCount;
            /// <summary>Bytes written.</summary>
            public ulong WriteTransferCount;
            /// <summary>Other bytes.</summary>
            public ulong OtherTransferCount;
        }

        /// <summary>
        /// PROCESS_BASIC_INFORMATION.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_BASIC_INFORMATION
        {
            /// <summary>Exit status.</summary>
            public IntPtr ExitStatus;
            /// <summary>PEB address.</summary>
            public IntPtr PebBaseAddress;
            /// <summary>Affinity mask.</summary>
            public IntPtr AffinityMask;
            /// <summary>Base priority.</summary>
            public IntPtr BasePriority;
            /// <summary>Process ID.</summary>
            public IntPtr UniqueProcessId;
            /// <summary>Parent process ID.</summary>
            public IntPtr InheritedFromUniqueProcessId;
        }

        /// <summary>
        /// Access right sufficient for image name, times, I/O counters, and basic information.
        /// </summary>
        public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        /// <summary>
        /// Access right for NtSuspendProcess / NtResumeProcess.
        /// </summary>
        public const uint PROCESS_SUSPEND_RESUME = 0x0800;

        #endregion

        #region Private-Members

        private const int ProcessBasicInformation = 0;
        private const int ProcessCommandLineInformation = 60;
        private const int SystemProcessorPerformanceInformation = 8;
        private const int SystemProcessorPerformanceInformationSize = 48;
        private const uint STATUS_INFO_LENGTH_MISMATCH = 0xC0000004;
        private const uint TOKEN_QUERY = 0x0008;
        private const int TokenUser = 1;

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

        /// <summary>
        /// Milliseconds since boot.
        /// </summary>
        [DllImport("kernel32.dll")]
        public static extern ulong GetTickCount64();

        /// <summary>
        /// Total physical memory in bytes, or null.
        /// </summary>
        /// <returns>Bytes.</returns>
        public static long? GetTotalPhysicalMemory()
        {
            MEMORYSTATUSEX status;
            return TryGetMemoryStatus(out status) ? (long?)status.ullTotalPhys : null;
        }

        /// <summary>
        /// Read the memory status.
        /// </summary>
        /// <param name="status">Status.</param>
        /// <returns>True if read.</returns>
        public static bool TryGetMemoryStatus(out MEMORYSTATUSEX status)
        {
            status = new MEMORYSTATUSEX();
            status.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            return GlobalMemoryStatusEx(ref status);
        }

        /// <summary>
        /// Per-processor idle, kernel (including idle), and user times.
        /// </summary>
        /// <param name="times">Times.</param>
        /// <returns>True if read.</returns>
        public static bool TryReadPerCoreCpuTimes(out CpuTimes[] times)
        {
            times = null;
            int count = Environment.ProcessorCount;
            int size = count * SystemProcessorPerformanceInformationSize;
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                int returned;
                if (NtQuerySystemInformation(SystemProcessorPerformanceInformation, buffer, size, out returned) != 0) return false;

                count = returned / SystemProcessorPerformanceInformationSize;
                times = new CpuTimes[count];
                for (int i = 0; i < count; i++)
                {
                    IntPtr entry = IntPtr.Add(buffer, i * SystemProcessorPerformanceInformationSize);
                    long idle = Marshal.ReadInt64(entry, 0);
                    long kernel = Marshal.ReadInt64(entry, 8);
                    long user = Marshal.ReadInt64(entry, 16);
                    times[i] = new CpuTimes((ulong)(kernel + user), (ulong)idle);
                }

                return count > 0;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        /// <summary>
        /// Full image path of a process.
        /// </summary>
        /// <param name="pid">Process ID.</param>
        /// <returns>Path or empty.</returns>
        public static string GetImagePath(int pid)
        {
            using (SafeProcessHandle handle = Open(pid, PROCESS_QUERY_LIMITED_INFORMATION))
            {
                if (handle == null) return string.Empty;
                StringBuilder sb = new StringBuilder(1024);
                int size = sb.Capacity;
                return QueryFullProcessImageName(handle.Handle, 0, sb, ref size) ? sb.ToString(0, size) : string.Empty;
            }
        }

        /// <summary>
        /// Parent process ID.
        /// </summary>
        /// <param name="pid">Process ID.</param>
        /// <returns>Parent ID or null.</returns>
        public static int? GetParentProcessId(int pid)
        {
            using (SafeProcessHandle handle = Open(pid, PROCESS_QUERY_LIMITED_INFORMATION))
            {
                if (handle == null) return null;
                PROCESS_BASIC_INFORMATION info = new PROCESS_BASIC_INFORMATION();
                int returned;
                int rc = NtQueryInformationProcess(handle.Handle, ProcessBasicInformation, ref info, Marshal.SizeOf(typeof(PROCESS_BASIC_INFORMATION)), out returned);
                return rc == 0 ? (int?)info.InheritedFromUniqueProcessId.ToInt64() : null;
            }
        }

        /// <summary>
        /// Command line of a process (Windows 8.1 and later).
        /// </summary>
        /// <param name="pid">Process ID.</param>
        /// <returns>Command line or empty.</returns>
        public static string GetCommandLine(int pid)
        {
            using (SafeProcessHandle handle = Open(pid, PROCESS_QUERY_LIMITED_INFORMATION))
            {
                if (handle == null) return string.Empty;

                int size = 4096;
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    IntPtr buffer = Marshal.AllocHGlobal(size);
                    try
                    {
                        int returned;
                        uint rc = (uint)NtQueryInformationProcessBuffer(handle.Handle, ProcessCommandLineInformation, buffer, size, out returned);
                        if (rc == STATUS_INFO_LENGTH_MISMATCH && returned > size)
                        {
                            size = returned;
                            continue;
                        }

                        if (rc != 0) return string.Empty;

                        // UNICODE_STRING { USHORT Length; USHORT MaximumLength; PWSTR Buffer; }
                        int length = (ushort)Marshal.ReadInt16(buffer, 0);
                        IntPtr text = Marshal.ReadIntPtr(buffer, IntPtr.Size);
                        return text == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUni(text, length / 2);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }

                return string.Empty;
            }
        }

        /// <summary>
        /// Owning user of a process as DOMAIN\user.
        /// </summary>
        /// <param name="pid">Process ID.</param>
        /// <returns>User or empty.</returns>
        public static string GetUserName(int pid)
        {
            using (SafeProcessHandle handle = Open(pid, PROCESS_QUERY_LIMITED_INFORMATION))
            {
                if (handle == null) return string.Empty;

                IntPtr token;
                if (!OpenProcessToken(handle.Handle, TOKEN_QUERY, out token)) return string.Empty;
                try
                {
                    int length;
                    GetTokenInformation(token, TokenUser, IntPtr.Zero, 0, out length);
                    if (length <= 0) return string.Empty;

                    IntPtr info = Marshal.AllocHGlobal(length);
                    try
                    {
                        if (!GetTokenInformation(token, TokenUser, info, length, out length)) return string.Empty;

                        // TOKEN_USER { SID_AND_ATTRIBUTES User { PSID Sid; DWORD Attributes; } }
                        IntPtr sid = Marshal.ReadIntPtr(info);
                        StringBuilder name = new StringBuilder(256);
                        StringBuilder domain = new StringBuilder(256);
                        int nameLength = name.Capacity;
                        int domainLength = domain.Capacity;
                        int use;
                        if (!LookupAccountSid(null, sid, name, ref nameLength, domain, ref domainLength, out use)) return string.Empty;
                        return domain.Length > 0 ? domain + "\\" + name : name.ToString();
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(info);
                    }
                }
                finally
                {
                    CloseHandle(token);
                }
            }
        }

        /// <summary>
        /// Cumulative bytes read and written by a process (all I/O, including files and devices).
        /// </summary>
        /// <param name="pid">Process ID.</param>
        /// <param name="readBytes">Bytes read.</param>
        /// <param name="writeBytes">Bytes written.</param>
        /// <returns>True if read.</returns>
        public static bool TryGetIoBytes(int pid, out long readBytes, out long writeBytes)
        {
            readBytes = 0;
            writeBytes = 0;
            using (SafeProcessHandle handle = Open(pid, PROCESS_QUERY_LIMITED_INFORMATION))
            {
                if (handle == null) return false;
                IO_COUNTERS counters;
                if (!GetProcessIoCounters(handle.Handle, out counters)) return false;
                readBytes = (long)counters.ReadTransferCount;
                writeBytes = (long)counters.WriteTransferCount;
                return true;
            }
        }

        /// <summary>
        /// Suspend or resume every thread in a process.
        /// </summary>
        /// <param name="pid">Process ID.</param>
        /// <param name="suspend">True to suspend, false to resume.</param>
        /// <returns>Result.</returns>
        public static ProcessActionResultEnum SuspendOrResume(int pid, bool suspend)
        {
            IntPtr handle = OpenProcess(PROCESS_SUSPEND_RESUME, false, pid);
            if (handle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                if (error == 5) return ProcessActionResultEnum.AccessDenied;
                if (error == 87) return ProcessActionResultEnum.NotFound;
                return ProcessActionResultEnum.Failed;
            }

            try
            {
                int rc = suspend ? NtSuspendProcess(handle) : NtResumeProcess(handle);
                return rc == 0 ? ProcessActionResultEnum.Success : ProcessActionResultEnum.Failed;
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        #endregion

        #region Private-Methods

        private static SafeProcessHandle Open(int pid, uint access)
        {
            IntPtr handle = OpenProcess(access, false, pid);
            return handle == IntPtr.Zero ? null : new SafeProcessHandle(handle);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "QueryFullProcessImageNameW")]
        private static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder exeName, ref int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetProcessIoCounters(IntPtr process, out IO_COUNTERS counters);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(IntPtr token, int infoClass, IntPtr info, int length, out int returnLength);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "LookupAccountSidW")]
        private static extern bool LookupAccountSid(string systemName, IntPtr sid, StringBuilder name, ref int nameLength, StringBuilder domain, ref int domainLength, out int use);

        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(IntPtr process, int infoClass, ref PROCESS_BASIC_INFORMATION info, int length, out int returnLength);

        [DllImport("ntdll.dll", EntryPoint = "NtQueryInformationProcess")]
        private static extern int NtQueryInformationProcessBuffer(IntPtr process, int infoClass, IntPtr info, int length, out int returnLength);

        [DllImport("ntdll.dll")]
        private static extern int NtQuerySystemInformation(int infoClass, IntPtr info, int length, out int returnLength);

        [DllImport("ntdll.dll")]
        private static extern int NtSuspendProcess(IntPtr process);

        [DllImport("ntdll.dll")]
        private static extern int NtResumeProcess(IntPtr process);

        #endregion

        #region Private-Classes

        /// <summary>
        /// Closes a process handle on dispose.
        /// </summary>
        private sealed class SafeProcessHandle : IDisposable
        {
            /// <summary>
            /// Handle.
            /// </summary>
            public IntPtr Handle { get; private set; }

            /// <summary>
            /// Instantiate.
            /// </summary>
            /// <param name="handle">Handle.</param>
            public SafeProcessHandle(IntPtr handle)
            {
                Handle = handle;
            }

            /// <summary>
            /// Close the handle.
            /// </summary>
            public void Dispose()
            {
                if (Handle != IntPtr.Zero) CloseHandle(Handle);
                Handle = IntPtr.Zero;
            }
        }

        #endregion
    }
}
