using System;

namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// Compact numeric sample of one process, used for history. NaN and -1 encode "unavailable".
    /// </summary>
    internal struct ProcessSample
    {
        #region Public-Members

        public DateTime TimestampUtc;
        public float CpuPercent;
        public float DiskReadBytesPerSecond;
        public float DiskWriteBytesPerSecond;
        public long WorkingSetBytes;
        public long PrivateBytes;
        public int HandleCount;
        public int ThreadCount;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Build from a snapshot.
        /// </summary>
        /// <param name="timestampUtc">Time.</param>
        /// <param name="p">Process.</param>
        /// <returns>Sample.</returns>
        public static ProcessSample From(DateTime timestampUtc, ProcessSnapshot p)
        {
            ProcessSample s = new ProcessSample();
            s.TimestampUtc = timestampUtc;
            s.CpuPercent = p.CpuPercent.HasValue ? (float)p.CpuPercent.Value : float.NaN;
            s.DiskReadBytesPerSecond = p.DiskReadBytesPerSecond.HasValue ? (float)p.DiskReadBytesPerSecond.Value : float.NaN;
            s.DiskWriteBytesPerSecond = p.DiskWriteBytesPerSecond.HasValue ? (float)p.DiskWriteBytesPerSecond.Value : float.NaN;
            s.WorkingSetBytes = p.WorkingSetBytes ?? -1;
            s.PrivateBytes = p.PrivateBytes ?? -1;
            s.HandleCount = p.HandleCount ?? -1;
            s.ThreadCount = p.ThreadCount ?? -1;
            return s;
        }

        /// <summary>
        /// Value for a process metric ID, or null.
        /// </summary>
        /// <param name="metricId">Metric ID.</param>
        /// <returns>Value.</returns>
        public double? Get(string metricId)
        {
            switch (metricId)
            {
                case MetricIds.ProcessCpu: return Float(CpuPercent);
                case MetricIds.ProcessDiskRead: return Float(DiskReadBytesPerSecond);
                case MetricIds.ProcessDiskWrite: return Float(DiskWriteBytesPerSecond);
                case MetricIds.ProcessWorkingSet: return WorkingSetBytes < 0 ? (double?)null : WorkingSetBytes;
                case MetricIds.ProcessPrivate: return PrivateBytes < 0 ? (double?)null : PrivateBytes;
                case MetricIds.ProcessHandles: return HandleCount < 0 ? (double?)null : HandleCount;
                case MetricIds.ProcessThreads: return ThreadCount < 0 ? (double?)null : ThreadCount;
                default: return null;
            }
        }

        #endregion

        #region Private-Methods

        private static double? Float(float value)
        {
            return float.IsNaN(value) ? (double?)null : Math.Round(value, 2);
        }

        #endregion
    }
}
