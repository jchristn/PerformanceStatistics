using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PerformanceStatistics.Monitoring;
using TaskManager.Tui;

namespace Test.Tui
{
    /// <summary>
    /// A snapshot source driven by the test.
    /// </summary>
    public sealed class FakeSnapshotSource : ISnapshotSource
    {
        public event EventHandler<HostSnapshot> SnapshotReceived;

        public HostInfo HostInfo { get; set; } = new HostInfo { HostId = "fake", HostName = "fakehost", Platform = "Linux", OperatingSystem = "FakeOS 1.0", Architecture = "X64", CpuModel = "Fake CPU", LogicalProcessors = 4, TotalMemoryBytes = 8L << 30, Elevated = true };

        public string Description { get; } = "fake";

        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(1);

        public bool Paused { get; set; }

        public bool ActionsEnabled { get; set; } = true;

        public List<ProcessActionRequest> Requests { get; } = new List<ProcessActionRequest>();

        public void Start()
        {
        }

        public void Emit(HostSnapshot snapshot)
        {
            SnapshotReceived?.Invoke(this, snapshot);
        }

        public Task<ProcessActionResponse> ExecuteAsync(ProcessActionRequest request)
        {
            Requests.Add(request);
            return Task.FromResult(new ProcessActionResponse { RequestId = request.Id, Pid = request.Pid, Action = request.Action, Result = "Success", Message = "Done.", CompletedUtc = DateTime.UtcNow });
        }

        public void Dispose()
        {
        }

        public static HostSnapshot Snapshot(DateTime ts, params ProcessSnapshot[] processes)
        {
            HostSnapshot s = new HostSnapshot { HostId = "fake", TimestampUtc = ts, IntervalSeconds = 1 };
            s.System.CpuPercent = 37;
            s.System.PerCoreCpuPercent = new double[] { 10, 20, 30, 90 };
            s.System.MemoryTotalBytes = 8L << 30;
            s.System.MemoryUsedBytes = 3L << 30;
            s.System.MemoryAvailableBytes = 5L << 30;
            s.System.SwapTotalBytes = 2L << 30;
            s.System.SwapUsedBytes = 1L << 28;
            s.System.DiskReadBytesPerSecond = 1024 * 1024;
            s.System.DiskWriteBytesPerSecond = 2048;
            s.System.NetworkReceivedBytesPerSecond = 5000;
            s.System.NetworkSentBytesPerSecond = 7000;
            s.System.LoadAverage1 = 1.5;
            s.System.LoadAverage5 = 1.2;
            s.System.LoadAverage15 = 0.9;
            s.System.ProcessCount = processes.Length;
            s.System.UptimeSeconds = 3700;
            s.Processes = new List<ProcessSnapshot>(processes);
            s.Network.Add(new NetworkInterfaceSnapshot { Name = "eth0", Description = "Fake NIC", ReceivedBytesPerSecond = 5000, SentBytesPerSecond = 7000, SpeedBitsPerSecond = 1000000000 });
            s.Volumes.Add(new VolumeSnapshot { Name = "/", Format = "ext4", TotalBytes = 100L << 30, FreeBytes = 40L << 30, UsedBytes = 60L << 30 });
            s.TcpConnections.Add(new TcpConnectionSnapshot { Local = "10.0.0.1:443", Remote = "10.0.0.2:51515", State = "Established" });
            return s;
        }

        public static ProcessSnapshot Process(int pid, string name, double? cpu, long memory, string user = "alice")
        {
            return new ProcessSnapshot
            {
                Pid = pid, ParentPid = 1, Name = name, User = user, CpuPercent = cpu, WorkingSetBytes = memory, ThreadCount = 4, HandleCount = 12,
                ExecutablePath = "/usr/bin/" + name, CommandLine = "/usr/bin/" + name + " --serve", State = "Running", Priority = "Normal",
                StartTimeUtc = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc), DiskReadBytesPerSecond = 100, DiskWriteBytesPerSecond = 0
            };
        }
    }
}
