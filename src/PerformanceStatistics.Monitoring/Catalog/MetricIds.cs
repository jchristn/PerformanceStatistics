namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// Stable metric identifiers shared by the TUI, the server API, storage, and the web app.
    /// </summary>
    public static class MetricIds
    {
#pragma warning disable CS1591 // Names are self-describing; descriptions live in MetricCatalog.
        public const string SystemCpu = "system.cpu";
        public const string SystemCpuCorePrefix = "system.cpu.core.";
        public const string SystemMemoryUsed = "system.memory.used";
        public const string SystemMemoryAvailable = "system.memory.available";
        public const string SystemMemoryTotal = "system.memory.total";
        public const string SystemSwapUsed = "system.swap.used";
        public const string SystemSwapTotal = "system.swap.total";
        public const string SystemDiskReadBytes = "system.disk.read.bytes";
        public const string SystemDiskWriteBytes = "system.disk.write.bytes";
        public const string SystemDiskReadOps = "system.disk.read.ops";
        public const string SystemDiskWriteOps = "system.disk.write.ops";
        public const string SystemDiskReadQueue = "system.disk.read.queue";
        public const string SystemDiskWriteQueue = "system.disk.write.queue";
        public const string SystemDiskUsed = "system.disk.used";
        public const string SystemDiskFree = "system.disk.free";
        public const string SystemNetworkRx = "system.network.rx";
        public const string SystemNetworkTx = "system.network.tx";
        public const string SystemLoad1 = "system.load.1";
        public const string SystemLoad5 = "system.load.5";
        public const string SystemLoad15 = "system.load.15";
        public const string SystemProcesses = "system.processes";
        public const string SystemThreads = "system.threads";
        public const string SystemHandles = "system.handles";
        public const string SystemUptime = "system.uptime";

        public const string ProcessPid = "process.pid";
        public const string ProcessParentPid = "process.ppid";
        public const string ProcessName = "process.name";
        public const string ProcessUser = "process.user";
        public const string ProcessState = "process.state";
        public const string ProcessPriority = "process.priority";
        public const string ProcessPath = "process.path";
        public const string ProcessCommandLine = "process.cmdline";
        public const string ProcessStart = "process.start";
        public const string ProcessCpu = "process.cpu";
        public const string ProcessCpuTime = "process.cputime";
        public const string ProcessWorkingSet = "process.memory.workingset";
        public const string ProcessPrivate = "process.memory.private";
        public const string ProcessVirtual = "process.memory.virtual";
        public const string ProcessDiskRead = "process.disk.read";
        public const string ProcessDiskWrite = "process.disk.write";
        public const string ProcessDiskReadTotal = "process.disk.read.total";
        public const string ProcessDiskWriteTotal = "process.disk.write.total";
        public const string ProcessHandles = "process.handles";
        public const string ProcessThreads = "process.threads";

        public const string NetworkRx = "network.rx";
        public const string NetworkTx = "network.tx";
        public const string NetworkRxPackets = "network.rx.packets";
        public const string NetworkTxPackets = "network.tx.packets";
#pragma warning restore CS1591
    }
}
