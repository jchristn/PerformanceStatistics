using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace PerformanceStatistics.Internal
{
    /// <summary>
    /// libc interop shared by Linux and macOS.
    /// </summary>
    internal static class UnixNativeMethods
    {
        #region Public-Members

        /// <summary>
        /// errno: no such process.
        /// </summary>
        public const int ESRCH = 3;

        /// <summary>
        /// errno: operation not permitted.
        /// </summary>
        public const int EPERM = 1;

        #endregion

        #region Private-Members

        private static readonly object _PasswdLock = new object();
        private static readonly Dictionary<uint, string> _UserNames = new Dictionary<uint, string>();

        #endregion

        #region Public-Methods

        /// <summary>
        /// Send a signal to a process.
        /// </summary>
        [DllImport("libc", SetLastError = true)]
        public static extern int kill(int pid, int sig);

        /// <summary>
        /// Read a symbolic link target.
        /// </summary>
        [DllImport("libc", SetLastError = true)]
        public static extern IntPtr readlink(string path, byte[] buf, IntPtr bufsiz);

        /// <summary>
        /// Load averages over 1, 5, and 15 minutes.
        /// </summary>
        [DllImport("libc")]
        public static extern int getloadavg([Out] double[] loadavg, int nelem);

        /// <summary>
        /// Password database entry for a user ID (not thread-safe; use <see cref="GetUserName"/>).
        /// </summary>
        [DllImport("libc")]
        private static extern IntPtr getpwuid(uint uid);

        /// <summary>
        /// SIGSTOP for the current operating system (19 on Linux, 17 on macOS).
        /// </summary>
        /// <returns>Signal number.</returns>
        public static int SigStop()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 17 : 19;
        }

        /// <summary>
        /// SIGCONT for the current operating system (18 on Linux, 19 on macOS).
        /// </summary>
        /// <returns>Signal number.</returns>
        public static int SigCont()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 19 : 18;
        }

        /// <summary>
        /// Resolve a user ID to a name (cached); falls back to the numeric ID.
        /// </summary>
        /// <param name="uid">User ID.</param>
        /// <returns>Name.</returns>
        public static string GetUserName(uint uid)
        {
            lock (_PasswdLock)
            {
                string name;
                if (_UserNames.TryGetValue(uid, out name)) return name;

                name = uid.ToString(System.Globalization.CultureInfo.InvariantCulture);
                try
                {
                    // struct passwd begins with char *pw_name on Linux and macOS.
                    IntPtr entry = getpwuid(uid);
                    if (entry != IntPtr.Zero)
                    {
                        IntPtr namePtr = Marshal.ReadIntPtr(entry);
                        if (namePtr != IntPtr.Zero) name = Marshal.PtrToStringAnsi(namePtr) ?? name;
                    }
                }
                catch
                {
                    // Keep the numeric ID.
                }

                _UserNames[uid] = name;
                return name;
            }
        }

        /// <summary>
        /// Read a symbolic link, or return null.
        /// </summary>
        /// <param name="path">Link path.</param>
        /// <returns>Target or null.</returns>
        public static string ReadLink(string path)
        {
            byte[] buffer = new byte[4096];
            long length = readlink(path, buffer, (IntPtr)buffer.Length).ToInt64();
            if (length <= 0) return null;
            return Encoding.UTF8.GetString(buffer, 0, (int)Math.Min(length, buffer.Length));
        }

        #endregion
    }
}
