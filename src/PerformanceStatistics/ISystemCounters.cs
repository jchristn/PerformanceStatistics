using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace PerformanceStatistics
{
    /// <summary>
    /// Base class for system counters.
    /// </summary>
    public abstract class ISystemCounters
    {
        #region Public-Members

        /// <summary>
        /// CPU utilization percentage.
        /// </summary>
        public double CpuUtilizationPercent { get; set; } = 0;

        /// <summary>
        /// Memory free in Megabytes.
        /// </summary>
        public double MemoryFreeMegabytes { get; set; } = 0;

        /// <summary>
        /// Total disk read operations.
        /// </summary>
        public int TotalDiskReadOperations { get; set; } = 0;

        /// <summary>
        /// Total disk write operations.
        /// </summary>
        public int TotalDiskWriteOperations { get; set; } = 0;

        /// <summary>
        /// Total disk read queue.
        /// </summary>
        public int TotalDiskReadQueue { get; set; } = 0;

        /// <summary>
        /// Total disk write queue.
        /// </summary>
        public int TotalDiskWriteQueue { get; set; } = 0;

        /// <summary>
        /// Total disk free percent.
        /// </summary>
        public double TotalDiskFreePercent { get; set; } = 0;

        /// <summary>
        /// Total disk free megabytes.
        /// </summary>
        public double TotalDiskFreeMegabytes { get; set; } = 0;

        /// <summary>
        /// Total disk size megabytes.
        /// </summary>
        public double TotalDiskSizeMegabytes { get; set; } = 0;

        /// <summary>
        /// Total disk used megabytes.
        /// </summary>
        public double TotalDiskUsedMegabytes { get; set; } = 0;

        #endregion

        #region Private-Members

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}