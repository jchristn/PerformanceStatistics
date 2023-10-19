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
    /// Windows performance statistics.
    /// </summary>
    public class WindowsPerformanceStatistics : IPerformanceStatistics
    {
        #region Public-Members

        /// <summary>
        /// CPU utilization percentage.
        /// </summary>
        public new double CpuUtilizationPercent
        {
            get
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new NotSupportedException("This library and class are only supported on Windows operating systems.");
                return Math.Round((new PerformanceCounter("Processor", "% Processor Time", "_Total")).NextValue(), 0);
            }
        }

        /// <summary>
        /// Memory free in Megabytes.
        /// </summary>
        public new double MemoryFreeMegabytes
        {
            get
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new NotSupportedException("This library and class are only supported on Windows operating systems.");
                return Math.Round((new PerformanceCounter("Memory", "Available MBytes")).NextValue(), 0);
            }
        }

        /// <summary>
        /// Total disk read operations.
        /// </summary>
        public new int TotalDiskReadOperations
        {
            get
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new NotSupportedException("This library and class are only supported on Windows operating systems.");
                return (int)Math.Round((new PerformanceCounter("LogicalDisk", "Disk Reads/sec", "_Total")).NextValue(), 0);
            }
        }

        /// <summary>
        /// Total disk write operations.
        /// </summary>
        public new int TotalDiskWriteOperations
        {
            get
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new NotSupportedException("This library and class are only supported on Windows operating systems.");
                return (int)Math.Round((new PerformanceCounter("LogicalDisk", "Disk Writes/sec", "_Total")).NextValue(), 0);
            }
        }

        /// <summary>
        /// Total disk read queue.
        /// </summary>
        public new int TotalDiskReadQueue
        {
            get
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new NotSupportedException("This library and class are only supported on Windows operating systems.");
                return (int)Math.Round((new PerformanceCounter("LogicalDisk", "Avg. Disk Read Queue Length", "_Total")).NextValue(), 0);
            }
        }

        /// <summary>
        /// Total disk write queue.
        /// </summary>
        public new int TotalDiskWriteQueue
        {
            get
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new NotSupportedException("This library and class are only supported on Windows operating systems.");
                return (int)Math.Round((new PerformanceCounter("LogicalDisk", "Avg. Disk Write Queue Length", "_Total")).NextValue(), 0);
            }
        }

        /// <summary>
        /// Total disk free percent.
        /// </summary>
        public new double TotalDiskFreePercent
        {
            get
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new NotSupportedException("This library and class are only supported on Windows operating systems.");
                return Math.Round((new PerformanceCounter("LogicalDisk", "% Free Space", "_Total")).NextValue(), 0);
            }
        }

        /// <summary>
        /// Total disk free megabytes.
        /// </summary>
        public new double TotalDiskFreeMegabytes
        {
            get
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new NotSupportedException("This library and class are only supported on Windows operating systems.");
                return Math.Round((new PerformanceCounter("LogicalDisk", "Free Megabytes", "_Total")).NextValue(), 0);
            }
        }

        /// <summary>
        /// Total disk size megabytes.
        /// </summary>
        public new double TotalDiskSizeMegabytes
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
        public new double TotalDiskUsedMegabytes
        {
            get
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new NotSupportedException("This library and class are only supported on Windows operating systems.");
                return Convert.ToDouble(string.Format("{0:N2}", (TotalDiskSizeMegabytes - TotalDiskFreeMegabytes)));
            }
        }

        /// <summary>
        /// Active TCP connections.
        /// </summary>
        public new TcpConnectionInformation[] ActiveTcpConnections
        {
            get
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new NotSupportedException("This library and class are only supported on Windows operating systems.");
                TcpConnectionInformation[] ret = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();
                if (ret == null) return new TcpConnectionInformation[0];
                return ret;
            }
        }

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public WindowsPerformanceStatistics()
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
            sb.Append("Windows Performance Statistics" + Environment.NewLine);
            sb.Append("------------------------------" + Environment.NewLine);
            sb.Append("  CPU Utilization Percent         : " + CpuUtilizationPercent +"%" + Environment.NewLine);
            sb.Append("  Memory Free (Megabytes)         : " + MemoryFreeMegabytes + "MB" + Environment.NewLine);
            sb.Append("  Total Disk Read Operations      : " + TotalDiskReadOperations + Environment.NewLine);
            sb.Append("  Total Disk Write Operations     : " + TotalDiskWriteOperations + Environment.NewLine);
            sb.Append("  Total Disk Read Queue           : " + TotalDiskReadQueue + Environment.NewLine);
            sb.Append("  Total Disk Write Queue          : " + TotalDiskWriteQueue + Environment.NewLine);
            sb.Append("  Total Disk Free Percent         : " + TotalDiskFreePercent + "%" + Environment.NewLine);
            sb.Append("  Total Disk Free Megabytes       : " + TotalDiskFreeMegabytes + "MB" + Environment.NewLine);
            sb.Append("  Total Disk Size Megabytes       : " + TotalDiskSizeMegabytes + "MB" + Environment.NewLine);
            sb.Append("  Total Disk Used Megabytes       : " + TotalDiskUsedMegabytes + "MB" + Environment.NewLine);
            sb.Append("  Active TCP Connections          : " + ActiveTcpConnections.Length + Environment.NewLine);

            if (ActiveTcpConnections.Length > 0)
            {
                for (int i = 0; i < ActiveTcpConnections.Length; i++)
                {
                    sb.Append(
                        "  | " + 
                        ActiveTcpConnections[i].LocalEndPoint.ToString() + 
                        " to " + 
                        ActiveTcpConnections[i].RemoteEndPoint.ToString() + 
                        ": " + 
                        ActiveTcpConnections[i].State.ToString() + 
                        Environment.NewLine);
                }
            }

            return sb.ToString();
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}