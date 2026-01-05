using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Runtime.InteropServices;

namespace PerformanceStatistics
{
    /// <summary>
    /// Windows process counters.
    /// </summary>
    public class WindowsProcessCounters : IDisposable
    {
        private bool _Disposed = false;
        #region Public-Members

        /// <summary>
        /// Process ID.
        /// </summary>
        public int? ProcessId
        {
            get
            {
                return _Process.Id;
            }
        }

        /// <summary>
        /// Process name.
        /// </summary>
        public string ProcessName
        {
            get
            {
                return _Process.ProcessName;
            }
        }

        /// <summary>
        /// Process title.
        /// </summary>
        public string ProcessTitle
        {
            get
            {
                return _Process.MainWindowTitle;
            }
        }

        /// <summary>
        /// Machine name.
        /// </summary>
        public string MachineName
        {
            get
            {
                return _Process.MachineName;
            }
        }

        /// <summary>
        /// CPU utilization percentage.
        /// </summary>
        public double CpuUtilizationPercent
        {
            get
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new NotSupportedException("This library and class are only supported on Windows operating systems.");
                using (var pc = new PerformanceCounter())
                {
                    pc.CategoryName = "Process";
                    pc.CounterName = "% Processor Time";
                    pc.InstanceName = _Process.ProcessName;
                    pc.NextValue();
                    double d = pc.NextValue();
                    return Convert.ToDouble(string.Format("{0:N2}", d));
                }
            }
        }

        /// <summary>
        /// Handle count.
        /// </summary>
        public int HandleCount
        {
            get
            {
                return _Process.HandleCount;
            }
        }

        /// <summary>
        /// Thread count.
        /// </summary>
        public int ThreadCount
        {
            get
            {
                if (_Process.Threads != null) return _Process.Threads.Count;
                return 0;
            }
        }

        /// <summary>
        /// Non-paged system memory.
        /// </summary>
        public long NonPagedSystemMemory
        {
            get
            {
                return _Process.NonpagedSystemMemorySize64;
            }
        }

        /// <summary>
        /// Paged system memory.
        /// </summary>
        public long PagedSystemMemory
        {
            get
            {
                return _Process.PagedSystemMemorySize64;
            }
        }

        /// <summary>
        /// Private memory.
        /// </summary>
        public long PrivateMemory
        {
            get
            {
                return _Process.PrivateMemorySize64;
            }
        }

        /// <summary>
        /// Virtual memory.
        /// </summary>
        public long VirtualMemory
        {
            get
            {
                return _Process.VirtualMemorySize64;
            }
        }

        /// <summary>
        /// Working set memory.
        /// </summary>
        public long WorkingSetMemory
        {
            get
            {
                return _Process.WorkingSet64;
            }
        }

        /// <summary>
        /// Peak paged system memory.
        /// </summary>
        public long PeakPagedSystemMemory
        {
            get
            {
                return _Process.PeakPagedMemorySize64;
            }
        }

        /// <summary>
        /// Peak virtual system memory.
        /// </summary>
        public long PeakVirtualSystemMemory
        {
            get
            {
                return _Process.PeakVirtualMemorySize64;
            }
        }

        /// <summary>
        /// Peak working set memory.
        /// </summary>
        public long PeakWorkingSetMemory
        {
            get
            {
                return _Process.PeakWorkingSet64;
            }
        }

        #endregion

        #region Private-Members

        private Process _Process = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public WindowsProcessCounters(Process process)
        {
            if (process == null) throw new ArgumentNullException(nameof(process));
            _Process = process;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Produce a human-readable object.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("  Process ID                    : " + ProcessId + Environment.NewLine);
            sb.Append("  Process Name                  : " + ProcessName + Environment.NewLine);
            sb.Append("  Process Title                 : " + ProcessTitle + Environment.NewLine);
            sb.Append("  Machine Name                  : " + MachineName + Environment.NewLine);
            sb.Append("  CPU Utilization               : " + CpuUtilizationPercent + "%" + Environment.NewLine);
            sb.Append("  Handle Count                  : " + HandleCount + Environment.NewLine);
            sb.Append("  Thread Count                  : " + ThreadCount + Environment.NewLine);
            sb.Append("  Memory, Private               : " + PrivateMemory + " bytes" + Environment.NewLine);
            sb.Append("  Memory, Paged System          : " + PagedSystemMemory + " bytes" + Environment.NewLine);
            sb.Append("  Memory, Peak Paged System     : " + PeakPagedSystemMemory + " bytes" + Environment.NewLine);
            sb.Append("  Memory, Non-Paged System      : " + NonPagedSystemMemory + " bytes" + Environment.NewLine);
            sb.Append("  Memory, Virtual               : " + VirtualMemory + " bytes" + Environment.NewLine);
            sb.Append("  Memory, Peak Virtual          : " + PeakVirtualSystemMemory + " bytes" + Environment.NewLine);
            sb.Append("  Memory, Working Set           : " + WorkingSetMemory + " bytes" + Environment.NewLine);
            sb.Append("  Memory, Peak Working Set      : " + PeakWorkingSetMemory + " bytes" + Environment.NewLine);
            return sb.ToString();
        }

        #endregion

        #region Private-Methods

        #endregion

        #region IDisposable

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        /// <param name="disposing">True if disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;

            if (disposing)
            {
                _Process?.Dispose();
                _Process = null;
            }

            _Disposed = true;
        }

        #endregion
    }
}