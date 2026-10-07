using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using PerformanceStatistics.Internal;
using PerformanceStatistics.Windows;

namespace PerformanceStatistics
{
    /// <summary>
    /// Cross-platform process actions. Methods never throw; they return a <see cref="ProcessActionResultEnum"/>.
    /// Acting on another user's process usually requires elevation (Administrator or root).
    /// </summary>
    public static class ProcessActions
    {
        #region Public-Methods

        /// <summary>
        /// Terminate a process immediately, optionally with all of its descendants.
        /// </summary>
        /// <param name="pid">Process ID.</param>
        /// <param name="entireProcessTree">True to also terminate descendants.</param>
        /// <returns>Result.</returns>
        public static ProcessActionResultEnum Kill(int pid, bool entireProcessTree = false)
        {
            return Run(pid, p =>
            {
#if NETCOREAPP3_0_OR_GREATER
                p.Kill(entireProcessTree);
#else
                if (entireProcessTree) throw new PlatformNotSupportedException();
                p.Kill();
#endif
            });
        }

        /// <summary>
        /// Set a process's priority on the cross-platform scale.
        /// </summary>
        /// <param name="pid">Process ID.</param>
        /// <param name="priority">Priority.</param>
        /// <returns>Result.</returns>
        public static ProcessActionResultEnum SetPriority(int pid, ProcessPriorityEnum priority)
        {
            return Run(pid, p => p.PriorityClass = ToPriorityClass(priority));
        }

        /// <summary>
        /// Suspend (stop) a process. Unix: SIGSTOP. Windows: NtSuspendProcess.
        /// </summary>
        /// <param name="pid">Process ID.</param>
        /// <returns>Result.</returns>
        public static ProcessActionResultEnum Suspend(int pid)
        {
            return SuspendOrResume(pid, true);
        }

        /// <summary>
        /// Resume a suspended process. Unix: SIGCONT. Windows: NtResumeProcess.
        /// </summary>
        /// <param name="pid">Process ID.</param>
        /// <returns>Result.</returns>
        public static ProcessActionResultEnum Resume(int pid)
        {
            return SuspendOrResume(pid, false);
        }

        /// <summary>
        /// Map a .NET priority class to the cross-platform scale.
        /// </summary>
        /// <param name="priorityClass">Priority class.</param>
        /// <returns>Priority.</returns>
        public static ProcessPriorityEnum FromPriorityClass(ProcessPriorityClass priorityClass)
        {
            switch (priorityClass)
            {
                case ProcessPriorityClass.Idle: return ProcessPriorityEnum.Idle;
                case ProcessPriorityClass.BelowNormal: return ProcessPriorityEnum.BelowNormal;
                case ProcessPriorityClass.AboveNormal: return ProcessPriorityEnum.AboveNormal;
                case ProcessPriorityClass.High: return ProcessPriorityEnum.High;
                case ProcessPriorityClass.RealTime: return ProcessPriorityEnum.RealTime;
                default: return ProcessPriorityEnum.Normal;
            }
        }

        /// <summary>
        /// Map the cross-platform scale to a .NET priority class.
        /// </summary>
        /// <param name="priority">Priority.</param>
        /// <returns>Priority class.</returns>
        public static ProcessPriorityClass ToPriorityClass(ProcessPriorityEnum priority)
        {
            switch (priority)
            {
                case ProcessPriorityEnum.Idle: return ProcessPriorityClass.Idle;
                case ProcessPriorityEnum.BelowNormal: return ProcessPriorityClass.BelowNormal;
                case ProcessPriorityEnum.AboveNormal: return ProcessPriorityClass.AboveNormal;
                case ProcessPriorityEnum.High: return ProcessPriorityClass.High;
                case ProcessPriorityEnum.RealTime: return ProcessPriorityClass.RealTime;
                default: return ProcessPriorityClass.Normal;
            }
        }

        #endregion

        #region Private-Methods

        private static ProcessActionResultEnum Run(int pid, Action<Process> action)
        {
            Process process;
            try
            {
                process = Process.GetProcessById(pid);
            }
            catch (ArgumentException)
            {
                return ProcessActionResultEnum.NotFound;
            }
            catch (Exception e)
            {
                return Classify(e);
            }

            using (process)
            {
                try
                {
                    action(process);
                    return ProcessActionResultEnum.Success;
                }
                catch (Exception e)
                {
                    return Classify(e);
                }
            }
        }

        private static ProcessActionResultEnum SuspendOrResume(int pid, bool suspend)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return WindowsNativeMethods.SuspendOrResume(pid, suspend);

                int rc = UnixNativeMethods.kill(pid, suspend ? UnixNativeMethods.SigStop() : UnixNativeMethods.SigCont());
                if (rc == 0) return ProcessActionResultEnum.Success;

                int errno = Marshal.GetLastWin32Error();
                if (errno == UnixNativeMethods.ESRCH) return ProcessActionResultEnum.NotFound;
                if (errno == UnixNativeMethods.EPERM) return ProcessActionResultEnum.AccessDenied;
                return ProcessActionResultEnum.Failed;
            }
            catch (Exception e)
            {
                return Classify(e);
            }
        }

        private static ProcessActionResultEnum Classify(Exception e)
        {
            if (e is PlatformNotSupportedException || e is NotSupportedException || e is DllNotFoundException || e is EntryPointNotFoundException)
                return ProcessActionResultEnum.NotSupported;
            if (e is InvalidOperationException || e is ArgumentException)
                return ProcessActionResultEnum.NotFound;
            if (e is UnauthorizedAccessException)
                return ProcessActionResultEnum.AccessDenied;

            Win32Exception win32 = e as Win32Exception;
            if (win32 != null)
            {
                // 5 = ERROR_ACCESS_DENIED (Windows); 1 = EPERM and 13 = EACCES (Unix).
                if (win32.NativeErrorCode == 5 || win32.NativeErrorCode == 1 || win32.NativeErrorCode == 13)
                    return ProcessActionResultEnum.AccessDenied;
                if (win32.NativeErrorCode == 3) return ProcessActionResultEnum.NotFound;
            }

            return ProcessActionResultEnum.Failed;
        }

        #endregion
    }
}
