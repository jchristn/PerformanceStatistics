using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Diagnostics;

namespace PerformanceStatistics
{
    /// <summary>
    /// Base class for performance statistics.
    /// </summary>
    public abstract class IPerformanceStatistics
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

        /// <summary>
        /// Active TCP connections.
        /// </summary>
        public TcpConnectionInformation[] ActiveTcpConnections;

        #endregion

        #region Private-Members

        #endregion

        #region Public-Methods

        /// <summary>
        /// Retrieve active TCP connections by port (source, destination, or both).
        /// </summary>
        /// <param name="sourcePort">Source port.</param>
        /// <param name="destPort">Destination port.</param>
        /// <returns>Array of TCP connections.</returns>
        public TcpConnectionInformation[] GetActiveTcpConnections(int? sourcePort = null, int? destPort = null)
        {
            if (sourcePort == null && destPort == null) return ActiveTcpConnections;

            if (sourcePort != null && destPort == null)
            {
                return ActiveTcpConnections.Where(c =>
                    c.LocalEndPoint.Port == sourcePort.Value).ToArray();
            }
            else if (sourcePort == null && destPort != null)
            {
                return ActiveTcpConnections.Where(c =>
                    c.RemoteEndPoint.Port == destPort.Value).ToArray();
            }
            else
            {
                return ActiveTcpConnections.Where(c =>
                    c.LocalEndPoint.Port == sourcePort.Value
                    && c.RemoteEndPoint.Port == destPort.Value).ToArray();
            }
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}