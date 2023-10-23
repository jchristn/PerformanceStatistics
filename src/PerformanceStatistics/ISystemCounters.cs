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
        public double CpuUtilizationPercent;

        /// <summary>
        /// Memory free in Megabytes.
        /// </summary>
        public double MemoryFreeMegabytes;

        /// <summary>
        /// Total disk read operations.
        /// </summary>
        public int TotalDiskReadOperations;

        /// <summary>
        /// Total disk write operations.
        /// </summary>
        public int TotalDiskWriteOperations;

        /// <summary>
        /// Total disk read queue.
        /// </summary>
        public int TotalDiskReadQueue;

        /// <summary>
        /// Total disk write queue.
        /// </summary>
        public int TotalDiskWriteQueue;

        /// <summary>
        /// Total disk free percent.
        /// </summary>
        public double TotalDiskFreePercent;

        /// <summary>
        /// Total disk free megabytes.
        /// </summary>
        public double TotalDiskFreeMegabytes;

        /// <summary>
        /// Total disk size megabytes.
        /// </summary>
        public double TotalDiskSizeMegabytes;

        /// <summary>
        /// Total disk used megabytes.
        /// </summary>
        public double TotalDiskUsedMegabytes;

        #endregion

        #region Private-Members

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}