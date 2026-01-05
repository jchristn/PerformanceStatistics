using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Diagnostics;
using System.Text;
using System.Runtime.InteropServices;

namespace PerformanceStatistics
{
    /// <summary>
    /// Windows system counters.
    /// </summary>
    public class WindowsSystemCounters
    {
        #region Public-Members

        /// <summary>
        /// CPU utilization percentage.
        /// </summary>
        public double CpuUtilizationPercent
        {
            get
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new NotSupportedException("This library and class are only supported on Windows operating systems.");
                using (var counter = new PerformanceCounter("Processor", "% Processor Time", "_Total"))
                {
                    return Math.Round(counter.NextValue(), 0);
                }
            }
        }

        /// <summary>
        /// Memory free in Megabytes.
        /// </summary>
        public double MemoryFreeMegabytes
        {
            get
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new NotSupportedException("This library and class are only supported on Windows operating systems.");
                using (var counter = new PerformanceCounter("Memory", "Available MBytes"))
                {
                    return Math.Round(counter.NextValue(), 0);
                }
            }
        }

        /// <summary>
        /// Total disk read operations.
        /// </summary>
        public int TotalDiskReadOperations
        {
            get
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new NotSupportedException("This library and class are only supported on Windows operating systems.");
                using (var counter = new PerformanceCounter("LogicalDisk", "Disk Reads/sec", "_Total"))
                {
                    return (int)Math.Round(counter.NextValue(), 0);
                }
            }
        }

        /// <summary>
        /// Total disk write operations.
        /// </summary>
        public int TotalDiskWriteOperations
        {
            get
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new NotSupportedException("This library and class are only supported on Windows operating systems.");
                using (var counter = new PerformanceCounter("LogicalDisk", "Disk Writes/sec", "_Total"))
                {
                    return (int)Math.Round(counter.NextValue(), 0);
                }
            }
        }

        /// <summary>
        /// Total disk read queue.
        /// </summary>
        public int TotalDiskReadQueue
        {
            get
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new NotSupportedException("This library and class are only supported on Windows operating systems.");
                using (var counter = new PerformanceCounter("LogicalDisk", "Avg. Disk Read Queue Length", "_Total"))
                {
                    return (int)Math.Round(counter.NextValue(), 0);
                }
            }
        }

        /// <summary>
        /// Total disk write queue.
        /// </summary>
        public int TotalDiskWriteQueue
        {
            get
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new NotSupportedException("This library and class are only supported on Windows operating systems.");
                using (var counter = new PerformanceCounter("LogicalDisk", "Avg. Disk Write Queue Length", "_Total"))
                {
                    return (int)Math.Round(counter.NextValue(), 0);
                }
            }
        }

        /// <summary>
        /// Total disk free percent.
        /// </summary>
        public double TotalDiskFreePercent
        {
            get
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new NotSupportedException("This library and class are only supported on Windows operating systems.");
                using (var counter = new PerformanceCounter("LogicalDisk", "% Free Space", "_Total"))
                {
                    return Math.Round(counter.NextValue(), 0);
                }
            }
        }

        /// <summary>
        /// Total disk free megabytes.
        /// </summary>
        public double TotalDiskFreeMegabytes
        {
            get
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new NotSupportedException("This library and class are only supported on Windows operating systems.");
                using (var counter = new PerformanceCounter("LogicalDisk", "Free Megabytes", "_Total"))
                {
                    return Math.Round(counter.NextValue(), 0);
                }
            }
        }

        /// <summary>
        /// Total disk size megabytes.
        /// </summary>
        public double TotalDiskSizeMegabytes
        {
            get
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new NotSupportedException("This library and class are only supported on Windows operating systems.");
                return Convert.ToDouble(string.Format("{0:N2}", ((TotalDiskFreeMegabytes / TotalDiskFreePercent) * 100)));
            }
        }

        /// <summary>
        /// Total disk used megabytes.
        /// </summary>
        public double TotalDiskUsedMegabytes
        {
            get
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new NotSupportedException("This library and class are only supported on Windows operating systems.");
                return Convert.ToDouble(string.Format("{0:N2}", (TotalDiskSizeMegabytes - TotalDiskFreeMegabytes)));
            }
        }

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public WindowsSystemCounters()
        {

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
            sb.Append("  CPU Utilization Percent       : " + CpuUtilizationPercent + "%" + Environment.NewLine);
            sb.Append("  Memory Free (Megabytes)       : " + MemoryFreeMegabytes + "MB" + Environment.NewLine);
            sb.Append("  Total Disk Read Operations    : " + TotalDiskReadOperations + Environment.NewLine);
            sb.Append("  Total Disk Write Operations   : " + TotalDiskWriteOperations + Environment.NewLine);
            sb.Append("  Total Disk Read Queue         : " + TotalDiskReadQueue + Environment.NewLine);
            sb.Append("  Total Disk Write Queue        : " + TotalDiskWriteQueue + Environment.NewLine);
            sb.Append("  Total Disk Free Percent       : " + TotalDiskFreePercent + "%" + Environment.NewLine);
            sb.Append("  Total Disk Free Megabytes     : " + TotalDiskFreeMegabytes + "MB" + Environment.NewLine);
            sb.Append("  Total Disk Size Megabytes     : " + TotalDiskSizeMegabytes + "MB" + Environment.NewLine);
            sb.Append("  Total Disk Used Megabytes     : " + TotalDiskUsedMegabytes + "MB" + Environment.NewLine);
            return sb.ToString();
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}