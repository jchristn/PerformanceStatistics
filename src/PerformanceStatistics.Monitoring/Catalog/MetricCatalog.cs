using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// The single source of truth for every metric: ID, name, unit, definition, per-platform availability, and
    /// how to read it from a snapshot. Both UIs build columns, charts, and "n/a" reasons from this catalog, and the
    /// server serves it at /api/v1/catalog.
    /// </summary>
    public static class MetricCatalog
    {
        #region Public-Members

        /// <summary>
        /// Every metric, in display order.
        /// </summary>
        public static IReadOnlyList<MetricDefinition> All
        {
            get { return _All; }
        }

        #endregion

        #region Private-Members

        private const string Windows = "Windows";
        private const string Linux = "Linux";
        private const string Mac = "Mac";
        private const string ElevationNote = "Hidden for other users' processes; admin/root privileges may be required (run as Administrator or with sudo).";

        private static readonly List<MetricDefinition> _All = Build();
        private static readonly Dictionary<string, MetricDefinition> _ById = _All.ToDictionary(m => m.Id, StringComparer.Ordinal);

        #endregion

        #region Public-Methods

        /// <summary>
        /// Look up a metric. Per-core IDs ("system.cpu.core.N") resolve to a definition named for that core.
        /// </summary>
        /// <param name="id">Metric ID.</param>
        /// <returns>Definition, or null if unknown.</returns>
        public static MetricDefinition Get(string id)
        {
            if (id == null) return null;
            MetricDefinition ret;
            if (_ById.TryGetValue(id, out ret)) return ret;

            int core;
            if (TryParseCore(id, out core))
            {
                return new MetricDefinition
                {
                    Id = id,
                    Name = "CPU core " + core,
                    ShortName = "CPU" + core,
                    Unit = MetricUnitEnum.Percent,
                    Scope = MetricScopeEnum.System,
                    Description = "Utilization of logical processor " + core + ", 0-100, averaged over the sampling interval.",
                    SystemSelector = s => s.PerCoreCpuPercent != null && core < s.PerCoreCpuPercent.Length ? s.PerCoreCpuPercent[core] : (double?)null
                };
            }

            return null;
        }

        /// <summary>
        /// Read a system metric from a snapshot.
        /// </summary>
        /// <param name="id">Metric ID.</param>
        /// <param name="snapshot">Snapshot.</param>
        /// <returns>Value or null.</returns>
        public static double? SystemValue(string id, SystemSnapshot snapshot)
        {
            if (snapshot == null) return null;
            MetricDefinition m = Get(id);
            return m != null && m.SystemSelector != null ? m.SystemSelector(snapshot) : null;
        }

        /// <summary>
        /// Read a numeric process metric from a snapshot.
        /// </summary>
        /// <param name="id">Metric ID.</param>
        /// <param name="process">Process.</param>
        /// <returns>Value or null.</returns>
        public static double? ProcessValue(string id, ProcessSnapshot process)
        {
            if (process == null) return null;
            MetricDefinition m = Get(id);
            return m != null && m.ProcessSelector != null ? m.ProcessSelector(process) : null;
        }

        /// <summary>
        /// Read a process metric as display text (formatted numbers, or text fields), "n/a" when unavailable.
        /// </summary>
        /// <param name="id">Metric ID.</param>
        /// <param name="process">Process.</param>
        /// <returns>Text.</returns>
        public static string ProcessDisplay(string id, ProcessSnapshot process)
        {
            MetricDefinition m = Get(id);
            if (m == null || process == null) return MetricFormatter.NotAvailable;
            if (m.ProcessText != null)
            {
                string text = m.ProcessText(process);
                return String.IsNullOrEmpty(text) ? MetricFormatter.NotAvailable : text;
            }

            return MetricFormatter.Format(m.ProcessSelector != null ? m.ProcessSelector(process) : null, m.Unit);
        }

        /// <summary>
        /// Read a network interface metric.
        /// </summary>
        /// <param name="id">Metric ID.</param>
        /// <param name="ni">Interface.</param>
        /// <returns>Value or null.</returns>
        public static double? InterfaceValue(string id, NetworkInterfaceSnapshot ni)
        {
            if (ni == null) return null;
            MetricDefinition m = Get(id);
            return m != null && m.InterfaceSelector != null ? m.InterfaceSelector(ni) : null;
        }

        /// <summary>
        /// Metrics of one scope.
        /// </summary>
        /// <param name="scope">Scope.</param>
        /// <returns>Definitions.</returns>
        public static List<MetricDefinition> ForScope(MetricScopeEnum scope)
        {
            return _All.Where(m => m.Scope == scope).ToList();
        }

        #endregion

        #region Private-Methods

        private static bool TryParseCore(string id, out int core)
        {
            core = -1;
            return id.StartsWith(MetricIds.SystemCpuCorePrefix, StringComparison.Ordinal)
                && Int32.TryParse(id.Substring(MetricIds.SystemCpuCorePrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out core);
        }

        private static Dictionary<string, MetricAvailability> Avail(params object[] entries)
        {
            // entries: platform, level, note, platform, level, note, ...
            Dictionary<string, MetricAvailability> ret = new Dictionary<string, MetricAvailability>(StringComparer.Ordinal);
            if (entries.Length % 3 != 0) throw new ArgumentException("Availability entries come in (platform, level, note) triples.");
            for (int i = 0; i < entries.Length; i += 3)
            {
                ret[(string)entries[i]] = new MetricAvailability { Level = (AvailabilityEnum)entries[i + 1], Note = (string)entries[i + 2] };
            }

            return ret;
        }

        private static MetricDefinition Sys(string id, string name, string shortName, MetricUnitEnum unit, string description, Func<SystemSnapshot, double?> selector, Dictionary<string, MetricAvailability> availability = null)
        {
            return new MetricDefinition { Id = id, Name = name, ShortName = shortName, Unit = unit, Scope = MetricScopeEnum.System, Description = description, SystemSelector = selector, Availability = availability ?? new Dictionary<string, MetricAvailability>() };
        }

        private static MetricDefinition Proc(string id, string name, string shortName, MetricUnitEnum unit, string description, Func<ProcessSnapshot, double?> selector, Dictionary<string, MetricAvailability> availability = null)
        {
            return new MetricDefinition { Id = id, Name = name, ShortName = shortName, Unit = unit, Scope = MetricScopeEnum.Process, Description = description, ProcessSelector = selector, Availability = availability ?? new Dictionary<string, MetricAvailability>() };
        }

        private static MetricDefinition ProcText(string id, string name, string shortName, string description, Func<ProcessSnapshot, string> text, Dictionary<string, MetricAvailability> availability = null)
        {
            return new MetricDefinition { Id = id, Name = name, ShortName = shortName, Unit = MetricUnitEnum.Text, Scope = MetricScopeEnum.Process, Description = description, ProcessText = text, Chartable = false, Availability = availability ?? new Dictionary<string, MetricAvailability>() };
        }

        private static MetricDefinition Net(string id, string name, string shortName, MetricUnitEnum unit, string description, Func<NetworkInterfaceSnapshot, double?> selector, Dictionary<string, MetricAvailability> availability = null)
        {
            return new MetricDefinition { Id = id, Name = name, ShortName = shortName, Unit = unit, Scope = MetricScopeEnum.Network, Description = description, InterfaceSelector = selector, Availability = availability ?? new Dictionary<string, MetricAvailability>() };
        }

        private static double? D(long? value)
        {
            return value.HasValue ? (double?)value.Value : null;
        }

        private static double? D(int? value)
        {
            return value.HasValue ? (double?)value.Value : null;
        }

        private static List<MetricDefinition> Build()
        {
            const AvailabilityEnum P = AvailabilityEnum.Partial;
            const AvailabilityEnum U = AvailabilityEnum.Unavailable;

            List<MetricDefinition> m = new List<MetricDefinition>();

            // System
            m.Add(Sys(MetricIds.SystemCpu, "CPU utilization", "CPU", MetricUnitEnum.Percent,
                "Busy time across all logical processors, 0-100 of total capacity, averaged over the sampling interval.", s => s.CpuPercent));
            m.Add(Sys(MetricIds.SystemMemoryUsed, "Memory used", "Mem used", MetricUnitEnum.Bytes,
                "Physical memory in use: total minus available.", s => D(s.MemoryUsedBytes)));
            m.Add(Sys(MetricIds.SystemMemoryAvailable, "Memory available", "Mem avail", MetricUnitEnum.Bytes,
                "Memory that can be allocated without swapping, including reclaimable cache.", s => D(s.MemoryAvailableBytes),
                Avail(Linux, AvailabilityEnum.Full, "MemAvailable from /proc/meminfo.", Mac, AvailabilityEnum.Full, "Physical memory minus app, wired, and compressed memory (Activity Monitor).", Windows, AvailabilityEnum.Full, "Available physical memory.")));
            m.Add(Sys(MetricIds.SystemMemoryTotal, "Memory total", "Mem total", MetricUnitEnum.Bytes, "Installed physical memory.", s => D(s.MemoryTotalBytes)));
            m.Add(Sys(MetricIds.SystemSwapUsed, "Swap used", "Swap", MetricUnitEnum.Bytes, "Swap (paging) space in use.", s => D(s.SwapUsedBytes),
                Avail(Windows, P, "Committed memory beyond physical memory in use (paging file).")));
            m.Add(Sys(MetricIds.SystemSwapTotal, "Swap total", "Swap total", MetricUnitEnum.Bytes, "Swap (paging) capacity.", s => D(s.SwapTotalBytes),
                Avail(Windows, P, "Commit limit beyond physical memory (paging file).", Mac, P, "macOS grows swap on demand; this is the current swap file size.")));
            m.Add(Sys(MetricIds.SystemDiskReadBytes, "Disk read", "Disk rd", MetricUnitEnum.BytesPerSecond, "Bytes read per second across physical disks.", s => s.DiskReadBytesPerSecond));
            m.Add(Sys(MetricIds.SystemDiskWriteBytes, "Disk write", "Disk wr", MetricUnitEnum.BytesPerSecond, "Bytes written per second across physical disks.", s => s.DiskWriteBytesPerSecond));
            m.Add(Sys(MetricIds.SystemDiskReadOps, "Disk read ops", "Rd ops", MetricUnitEnum.PerSecond, "Read operations per second across physical disks.", s => s.DiskReadOpsPerSecond));
            m.Add(Sys(MetricIds.SystemDiskWriteOps, "Disk write ops", "Wr ops", MetricUnitEnum.PerSecond, "Write operations per second across physical disks.", s => s.DiskWriteOpsPerSecond));
            m.Add(Sys(MetricIds.SystemDiskReadQueue, "Disk read queue", "Rd queue", MetricUnitEnum.Count, "Outstanding read requests.", s => s.DiskReadQueue,
                Avail(Windows, P, "Average queue length since the previous sample.", Linux, P, "Instantaneous in-flight requests.", Mac, U, "macOS does not expose disk queue length.")));
            m.Add(Sys(MetricIds.SystemDiskWriteQueue, "Disk write queue", "Wr queue", MetricUnitEnum.Count, "Outstanding write requests.", s => s.DiskWriteQueue,
                Avail(Windows, P, "Average queue length since the previous sample.", Linux, P, "Instantaneous in-flight requests.", Mac, U, "macOS does not expose disk queue length.")));
            m.Add(Sys(MetricIds.SystemDiskUsed, "Disk used", "Disk used", MetricUnitEnum.Bytes, "Capacity minus available space across counted volumes.",
                s => s.DiskTotalBytes.HasValue && s.DiskFreeBytes.HasValue ? (double?)(s.DiskTotalBytes.Value - s.DiskFreeBytes.Value) : null));
            m.Add(Sys(MetricIds.SystemDiskFree, "Disk free", "Disk free", MetricUnitEnum.Bytes, "Available space across counted volumes.", s => D(s.DiskFreeBytes)));
            m.Add(Sys(MetricIds.SystemNetworkRx, "Network received", "Net rx", MetricUnitEnum.BytesPerSecond, "Bytes received per second across non-loopback interfaces that are up.", s => s.NetworkReceivedBytesPerSecond));
            m.Add(Sys(MetricIds.SystemNetworkTx, "Network sent", "Net tx", MetricUnitEnum.BytesPerSecond, "Bytes sent per second across non-loopback interfaces that are up.", s => s.NetworkSentBytesPerSecond));
            m.Add(Sys(MetricIds.SystemLoad1, "Load average (1m)", "Load1", MetricUnitEnum.Count, "Average runnable (and on Linux, uninterruptible) tasks over one minute.", s => s.LoadAverage1,
                Avail(Windows, U, "Windows has no load average.")));
            m.Add(Sys(MetricIds.SystemLoad5, "Load average (5m)", "Load5", MetricUnitEnum.Count, "Load average over five minutes.", s => s.LoadAverage5, Avail(Windows, U, "Windows has no load average.")));
            m.Add(Sys(MetricIds.SystemLoad15, "Load average (15m)", "Load15", MetricUnitEnum.Count, "Load average over fifteen minutes.", s => s.LoadAverage15, Avail(Windows, U, "Windows has no load average.")));
            m.Add(Sys(MetricIds.SystemProcesses, "Processes", "Procs", MetricUnitEnum.Count, "Visible processes.", s => D(s.ProcessCount)));
            m.Add(Sys(MetricIds.SystemThreads, "Threads", "Threads", MetricUnitEnum.Count, "Threads across visible processes.", s => D(s.ThreadCount)));
            m.Add(Sys(MetricIds.SystemHandles, "Handles / descriptors", "Handles", MetricUnitEnum.Count, "Open handles (Windows) or file descriptors (Linux, macOS) across processes that report them.", s => D(s.HandleCount),
                Avail(Linux, P, "File descriptors; other users' processes need admin/root privileges.", Mac, P, "File descriptors; other users' processes need admin/root privileges.")));
            m.Add(Sys(MetricIds.SystemUptime, "Uptime", "Uptime", MetricUnitEnum.Seconds, "Time since boot.", s => s.UptimeSeconds));

            // Process (text)
            m.Add(ProcText(MetricIds.ProcessPid, "Process ID", "PID", "Process identifier.", p => p.Pid.ToString(CultureInfo.InvariantCulture)));
            m.Add(ProcText(MetricIds.ProcessName, "Name", "Name", "Process name.", p => p.Name));
            m.Add(ProcText(MetricIds.ProcessUser, "User", "User", "Owning user (Windows: DOMAIN\\user).", p => p.User));
            m.Add(ProcText(MetricIds.ProcessState, "State", "State", "Running, Sleeping, Waiting (uninterruptible I/O), Stopped, or Zombie.", p => p.State,
                Avail(Windows, P, "Running, or Stopped when every thread is suspended.", Mac, P, "macOS reports most live processes as Running.")));
            m.Add(ProcText(MetricIds.ProcessPriority, "Priority", "Prio", "Idle, BelowNormal, Normal, AboveNormal, High, or RealTime (nice on Unix).", p => p.Priority));
            m.Add(ProcText(MetricIds.ProcessParentPid, "Parent process ID", "PPID", "Parent process identifier.", p => p.ParentPid.HasValue ? p.ParentPid.Value.ToString(CultureInfo.InvariantCulture) : null));
            m.Add(ProcText(MetricIds.ProcessStart, "Start time", "Started", "When the process started (local time in UIs, UTC in the API).", p => p.StartTimeUtc.HasValue ? p.StartTimeUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : null));
            m.Add(ProcText(MetricIds.ProcessPath, "Executable path", "Path", "Full path of the executable.", p => p.ExecutablePath));
            m.Add(ProcText(MetricIds.ProcessCommandLine, "Command line", "Command", "Arguments the process was started with.", p => p.CommandLine,
                Avail(Mac, P, ElevationNote)));

            // Process (numeric)
            m.Add(Proc(MetricIds.ProcessCpu, "CPU utilization", "CPU", MetricUnitEnum.Percent, "CPU time over wall time divided by logical processors: 0-100 of total machine capacity.", p => p.CpuPercent,
                Avail(Mac, P, ElevationNote)));
            m.Add(Proc(MetricIds.ProcessCpuTime, "CPU time", "CPU time", MetricUnitEnum.Seconds, "Total CPU time consumed since start.", p => p.CpuTimeSeconds,
                Avail(Mac, P, ElevationNote)));
            m.Add(Proc(MetricIds.ProcessWorkingSet, "Memory (resident)", "Memory", MetricUnitEnum.Bytes, "Working set / resident set size.", p => D(p.WorkingSetBytes)));
            m.Add(Proc(MetricIds.ProcessPrivate, "Memory (private)", "Private", MetricUnitEnum.Bytes, "Private memory.", p => D(p.PrivateBytes),
                Avail(Mac, P, "Physical footprint (Activity Monitor's Memory column). " + ElevationNote, Linux, P, "Private memory as reported by .NET.")));
            m.Add(Proc(MetricIds.ProcessVirtual, "Memory (virtual)", "Virtual", MetricUnitEnum.Bytes, "Virtual address space size.", p => D(p.VirtualBytes)));
            m.Add(Proc(MetricIds.ProcessDiskRead, "Disk read", "Disk rd", MetricUnitEnum.BytesPerSecond, "Bytes read per second.", p => p.DiskReadBytesPerSecond,
                Avail(Windows, P, "All I/O, including files, devices, and network.", Linux, P, "Storage I/O. " + ElevationNote, Mac, P, "Storage I/O. " + ElevationNote)));
            m.Add(Proc(MetricIds.ProcessDiskWrite, "Disk write", "Disk wr", MetricUnitEnum.BytesPerSecond, "Bytes written per second.", p => p.DiskWriteBytesPerSecond,
                Avail(Windows, P, "All I/O, including files, devices, and network.", Linux, P, "Storage I/O. " + ElevationNote, Mac, P, "Storage I/O. " + ElevationNote)));
            m.Add(Proc(MetricIds.ProcessDiskReadTotal, "Disk read total", "Read tot", MetricUnitEnum.Bytes, "Bytes read since start.", p => D(p.DiskReadBytesTotal),
                Avail(Windows, P, "All I/O.", Linux, P, ElevationNote, Mac, P, ElevationNote)));
            m.Add(Proc(MetricIds.ProcessDiskWriteTotal, "Disk write total", "Write tot", MetricUnitEnum.Bytes, "Bytes written since start.", p => D(p.DiskWriteBytesTotal),
                Avail(Windows, P, "All I/O.", Linux, P, ElevationNote, Mac, P, ElevationNote)));
            m.Add(Proc(MetricIds.ProcessHandles, "Handles / descriptors", "Handles", MetricUnitEnum.Count, "Open handles (Windows) or file descriptors (Linux, macOS).", p => D(p.HandleCount),
                Avail(Linux, P, "File descriptors. " + ElevationNote, Mac, P, "File descriptors. " + ElevationNote)));
            m.Add(Proc(MetricIds.ProcessThreads, "Threads", "Threads", MetricUnitEnum.Count, "Thread count.", p => D(p.ThreadCount)));

            // Network
            m.Add(Net(MetricIds.NetworkRx, "Received", "Rx", MetricUnitEnum.BytesPerSecond, "Bytes received per second.", n => n.ReceivedBytesPerSecond));
            m.Add(Net(MetricIds.NetworkTx, "Sent", "Tx", MetricUnitEnum.BytesPerSecond, "Bytes sent per second.", n => n.SentBytesPerSecond));
            m.Add(Net(MetricIds.NetworkRxPackets, "Packets received", "Rx pkts", MetricUnitEnum.PerSecond, "Packets received per second.", n => n.ReceivedPacketsPerSecond));
            m.Add(Net(MetricIds.NetworkTxPackets, "Packets sent", "Tx pkts", MetricUnitEnum.PerSecond, "Packets sent per second.", n => n.SentPacketsPerSecond));

            return m;
        }

        #endregion
    }
}
