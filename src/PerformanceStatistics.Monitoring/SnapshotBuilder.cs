using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Reflection;

namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// Builds <see cref="HostSnapshot"/>s from an <see cref="IPerformanceStatistics"/>. This is the only code that
    /// reads the library, so every app sees identical values. Not thread-safe; <see cref="MetricsSampler"/> owns one.
    /// </summary>
    public sealed class SnapshotBuilder
    {
        #region Public-Members

        /// <summary>
        /// Host facts.
        /// </summary>
        public HostInfo HostInfo { get; private set; }

        /// <summary>
        /// Collect processes in each snapshot.
        /// </summary>
        public bool IncludeProcesses { get; set; } = true;

        /// <summary>
        /// Refresh TCP connections every N snapshots (they are comparatively expensive to enumerate).
        /// </summary>
        public int TcpEvery { get; set; } = 5;

        #endregion

        #region Private-Members

        private readonly IPerformanceStatistics _Stats;
        private DateTime? _PreviousUtc = null;
        private int _Count = 0;
        private List<TcpConnectionSnapshot> _Tcp = new List<TcpConnectionSnapshot>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="stats">Statistics source; not owned.</param>
        /// <param name="hostId">Host identifier; null uses the lower-cased machine name.</param>
        public SnapshotBuilder(IPerformanceStatistics stats, string hostId = null)
        {
            _Stats = stats ?? throw new ArgumentNullException(nameof(stats));
            HostInfo = BuildHostInfo(stats.SystemInfo, hostId);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Sample everything once.
        /// </summary>
        /// <returns>Snapshot.</returns>
        public HostSnapshot Build()
        {
            DateTime now = DateTime.UtcNow;
            HostSnapshot snapshot = new HostSnapshot();
            snapshot.HostId = HostInfo.HostId;
            snapshot.TimestampUtc = now;
            snapshot.IntervalSeconds = _PreviousUtc.HasValue ? Math.Round((now - _PreviousUtc.Value).TotalSeconds, 3) : 0;
            _PreviousUtc = now;

            ISystemCounters sys = _Stats.System;
            SystemSnapshot s = snapshot.System;
            s.CpuPercent = sys.CpuUtilizationPercent;
            s.PerCoreCpuPercent = ToArray(sys.PerCoreCpuUtilizationPercent);
            s.MemoryTotalBytes = MegabytesToBytes(sys.MemoryTotalMegabytes);
            s.MemoryUsedBytes = MegabytesToBytes(sys.MemoryUsedMegabytes);
            s.MemoryAvailableBytes = MegabytesToBytes(sys.MemoryFreeMegabytes);
            s.SwapTotalBytes = MegabytesToBytes(sys.SwapTotalMegabytes);
            s.SwapUsedBytes = MegabytesToBytes(sys.SwapUsedMegabytes);
            s.DiskReadOpsPerSecond = sys.TotalDiskReadOperations;
            s.DiskWriteOpsPerSecond = sys.TotalDiskWriteOperations;
            s.DiskReadBytesPerSecond = sys.TotalDiskReadBytesPerSecond;
            s.DiskWriteBytesPerSecond = sys.TotalDiskWriteBytesPerSecond;
            s.DiskReadQueue = sys.TotalDiskReadQueue;
            s.DiskWriteQueue = sys.TotalDiskWriteQueue;
            s.NetworkReceivedBytesPerSecond = sys.NetworkReceivedBytesPerSecond;
            s.NetworkSentBytesPerSecond = sys.NetworkSentBytesPerSecond;
            s.LoadAverage1 = sys.LoadAverage1;
            s.LoadAverage5 = sys.LoadAverage5;
            s.LoadAverage15 = sys.LoadAverage15;
            if (HostInfo.BootTimeUtc.HasValue) s.UptimeSeconds = Math.Round((now - HostInfo.BootTimeUtc.Value).TotalSeconds, 0);

            foreach (NetworkInterfaceCounters ni in sys.NetworkInterfaces)
            {
                snapshot.Network.Add(new NetworkInterfaceSnapshot
                {
                    Name = ni.Name,
                    Description = ni.Description,
                    SpeedBitsPerSecond = ni.SpeedBitsPerSecond,
                    ReceivedBytesPerSecond = ni.ReceivedBytesPerSecond,
                    SentBytesPerSecond = ni.SentBytesPerSecond,
                    ReceivedPacketsPerSecond = ni.ReceivedPacketsPerSecond,
                    SentPacketsPerSecond = ni.SentPacketsPerSecond
                });
            }

            long diskTotal = 0;
            long diskFree = 0;
            foreach (VolumeInfo v in sys.Volumes)
            {
                snapshot.Volumes.Add(new VolumeSnapshot { Name = v.Name, Format = v.Format, TotalBytes = v.TotalBytes, FreeBytes = v.FreeBytes, UsedBytes = v.UsedBytes });
                diskTotal += v.TotalBytes;
                diskFree += v.FreeBytes;
            }

            if (snapshot.Volumes.Count > 0)
            {
                s.DiskTotalBytes = diskTotal;
                s.DiskFreeBytes = diskFree;
            }

            if (IncludeProcesses) CollectProcesses(snapshot);

            if (_Count % Math.Max(1, TcpEvery) == 0) _Tcp = CollectTcp();
            snapshot.TcpConnections = _Tcp;
            _Count++;

            return snapshot;
        }

        #endregion

        #region Private-Methods

        private void CollectProcesses(HostSnapshot snapshot)
        {
            int processCount = 0;
            long threads = 0;
            long handles = 0;
            bool anyHandles = false;

            foreach (IProcessCounters p in _Stats.GetAllProcesses())
            {
                int? pid = p.ProcessId;
                if (!pid.HasValue) continue;

                ProcessSnapshot ps = new ProcessSnapshot();
                ps.Pid = pid.Value;
                ps.ParentPid = p.ParentProcessId;
                ps.Name = p.ProcessName;
                ps.ExecutablePath = p.ExecutablePath;
                ps.CommandLine = p.CommandLine;
                ps.User = p.UserName;
                ps.StartTimeUtc = p.StartTimeUtc;
                ps.State = p.State.ToString();
                ProcessPriorityEnum? priority = p.Priority;
                ps.Priority = priority.HasValue ? priority.Value.ToString() : null;
                ps.CpuPercent = p.CpuUtilizationPercent;
                TimeSpan? cpuTime = p.TotalProcessorTime;
                ps.CpuTimeSeconds = cpuTime.HasValue ? Math.Round(cpuTime.Value.TotalSeconds, 2) : (double?)null;
                ps.WorkingSetBytes = p.WorkingSetMemory;
                ps.PrivateBytes = p.PrivateMemory;
                ps.VirtualBytes = p.VirtualMemory;
                ps.DiskReadBytesPerSecond = p.DiskReadBytesPerSecond;
                ps.DiskWriteBytesPerSecond = p.DiskWriteBytesPerSecond;
                ps.DiskReadBytesTotal = p.DiskReadBytesTotal;
                ps.DiskWriteBytesTotal = p.DiskWriteBytesTotal;
                ps.HandleCount = p.HandleCount;
                ps.ThreadCount = p.ThreadCount;

                snapshot.Processes.Add(ps);
                processCount++;
                threads += ps.ThreadCount ?? 0;
                if (ps.HandleCount.HasValue)
                {
                    handles += ps.HandleCount.Value;
                    anyHandles = true;
                }
            }

            snapshot.System.ProcessCount = processCount;
            snapshot.System.ThreadCount = threads;
            snapshot.System.HandleCount = anyHandles ? handles : (long?)null;
        }

        private List<TcpConnectionSnapshot> CollectTcp()
        {
            List<TcpConnectionSnapshot> ret = new List<TcpConnectionSnapshot>();
            foreach (TcpConnectionInformation c in _Stats.ActiveTcpConnections)
            {
                try
                {
                    ret.Add(new TcpConnectionSnapshot
                    {
                        Local = c.LocalEndPoint.ToString(),
                        Remote = c.RemoteEndPoint.ToString(),
                        State = c.State.ToString()
                    });
                }
                catch
                {
                }
            }

            return ret;
        }

        private static HostInfo BuildHostInfo(SystemInfo info, string hostId)
        {
            HostInfo ret = new HostInfo();
            ret.HostName = info.MachineName;
            ret.HostId = String.IsNullOrWhiteSpace(hostId) ? (info.MachineName ?? "host").ToLowerInvariant() : hostId.Trim();
            ret.Platform = info.Platform.ToString();
            ret.OperatingSystem = info.OperatingSystem;
            ret.Architecture = info.Architecture;
            ret.CpuModel = info.CpuModel;
            ret.LogicalProcessors = info.LogicalProcessors;
            ret.TotalMemoryBytes = info.TotalMemoryBytes;
            ret.BootTimeUtc = info.BootTimeUtc;
            ret.Elevated = Environment.IsPrivilegedProcess;
            ret.CollectorVersion = typeof(SnapshotBuilder).Assembly.GetName().Version?.ToString() ?? string.Empty;
            return ret;
        }

        private static long? MegabytesToBytes(double? megabytes)
        {
            return megabytes.HasValue ? (long?)(megabytes.Value * 1024 * 1024) : null;
        }

        private static double[] ToArray(IReadOnlyList<double> values)
        {
            double[] ret = new double[values.Count];
            for (int i = 0; i < ret.Length; i++) ret[i] = values[i];
            return ret;
        }

        #endregion
    }
}
