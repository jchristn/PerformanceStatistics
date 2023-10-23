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
    public class WindowsProcessCounters : IProcessCounters
    {
        #region Public-Members

        /// <summary>
        /// Process ID.
        /// </summary>
        public new int? ProcessId
        {
            get
            {
                return _Process.Id;
            }
        }

        /// <summary>
        /// Process name.
        /// </summary>
        public new string ProcessName
        {
            get
            {
                return _Process.ProcessName;
            }
        }

        /// <summary>
        /// Process title.
        /// </summary>
        public new string ProcessTitle
        {
            get
            {
                return _Process.MainWindowTitle;
            }
        }

        /// <summary>
        /// Machine name.
        /// </summary>
        public new string MachineName
        {
            get
            {
                return _Process.MachineName;
            }
        }

        /// <summary>
        /// CPU utilization percentage.
        /// </summary>
        public new double CpuUtilizationPercent
        {
            get
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new NotSupportedException("This library and class are only supported on Windows operating systems.");
                PerformanceCounter pc = new PerformanceCounter();
                pc.CategoryName = "Process";
                pc.CounterName = "% Processor Time";
                pc.InstanceName = _Process.ProcessName;
                pc.NextValue();
                double d = pc.NextValue();
                return d;
            }
        }

        /// <summary>
        /// Handle count.
        /// </summary>
        public new int HandleCount
        {
            get
            {
                return _Process.HandleCount;
            }
        }

        /// <summary>
        /// Thread count.
        /// </summary>
        public new int ThreadCount
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
        public new long NonPagedSystemMemory
        {
            get
            {
                return _Process.NonpagedSystemMemorySize64;
            }
        }

        /// <summary>
        /// Paged system memory.
        /// </summary>
        public new long PagedSystemMemory
        {
            get
            {
                return _Process.PagedSystemMemorySize64;
            }
        }

        /// <summary>
        /// Private memory.
        /// </summary>
        public new long PrivateMemory
        {
            get
            {
                return _Process.PrivateMemorySize64;
            }
        }

        /// <summary>
        /// Virtual memory.
        /// </summary>
        public new long VirtualMemory
        {
            get
            {
                return _Process.VirtualMemorySize64;
            }
        }

        /// <summary>
        /// Working set memory.
        /// </summary>
        public new long WorkingSetMemory
        {
            get
            {
                return _Process.WorkingSet64;
            }
        }

        /// <summary>
        /// Peak paged system memory.
        /// </summary>
        public new long PeakPagedSystemMemory
        {
            get
            {
                return _Process.PeakPagedMemorySize64;
            }
        }

        /// <summary>
        /// Peak virtual system memory.
        /// </summary>
        public new long PeakVirtualSystemMemory
        {
            get
            {
                return _Process.PeakVirtualMemorySize64;
            }
        }

        /// <summary>
        /// Peak working set memory.
        /// </summary>
        public new long PeakWorkingSetMemory
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
    }
}