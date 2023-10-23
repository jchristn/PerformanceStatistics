using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace PerformanceStatistics
{
    /// <summary>
    /// Base class for process counters
    /// </summary>
    public abstract class IProcessCounters
    {
        #region Public-Members

        /// <summary>
        /// Process ID.
        /// </summary>
        public int? ProcessId { get; set; } = null;

        /// <summary>
        /// Process name.
        /// </summary>
        public string ProcessName { get; set; } = null;

        /// <summary>
        /// Process title.
        /// </summary>
        public string ProcessTitle { get; set; } = null;

        /// <summary>
        /// Machine name.
        /// </summary>
        public string MachineName { get; set; } = null;

        /// <summary>
        /// CPU utilization percentage.
        /// </summary>
        public double CpuUtilizationPercent { get; set; } = 0;

        /// <summary>
        /// Handle count.
        /// </summary>
        public int HandleCount { get; set; } = 0;

        /// <summary>
        /// Thread count.
        /// </summary>
        public int ThreadCount { get; set; } = 0;

        /// <summary>
        /// Non-paged system memory.
        /// </summary>
        public long NonPagedSystemMemory { get; set; } = 0;

        /// <summary>
        /// Paged system memory.
        /// </summary>
        public long PagedSystemMemory { get; set; } = 0;

        /// <summary>
        /// Private memory.
        /// </summary>
        public long PrivateMemory { get; set; } = 0;

        /// <summary>
        /// Virtual memory.
        /// </summary>
        public long VirtualMemory { get; set; } = 0;

        /// <summary>
        /// Working set memory.
        /// </summary>
        public long WorkingSetMemory { get; set; } = 0;

        /// <summary>
        /// Peak paged system memory.
        /// </summary>
        public long PeakPagedSystemMemory { get; set; } = 0;

        /// <summary>
        /// Peak virtual system memory.
        /// </summary>
        public long PeakVirtualSystemMemory { get; set; } = 0;

        /// <summary>
        /// Peak working set memory.
        /// </summary>
        public long PeakWorkingSetMemory { get; set; } = 0;

        #endregion

        #region Private-Members

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}