using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using PerformanceStatistics;
using Touchstone.Core;
using static Test.Shared.TestSupport;

namespace Test.Shared.Suites
{
    /// <summary>
    /// Tests for the task-manager metrics added in v2.2.0: all-process enumeration, process metadata,
    /// per-core CPU, memory totals, swap, network, volumes, load averages, host facts, and process actions.
    /// </summary>
    public static class ExtendedMetricsSuite
    {
        private const string Id = "Extended";

        /// <summary>
        /// Build the extended metrics test suite.
        /// </summary>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Case(Id, "SystemInfoPopulated", "SystemInfo reports machine, OS, architecture, processors, memory, and boot time", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        SystemInfo info = stats.SystemInfo;
                        Check.IsNotEmpty(info.MachineName, "MachineName");
                        Check.IsNotEmpty(info.OperatingSystem, "OperatingSystem");
                        Check.IsNotEmpty(info.Architecture, "Architecture");
                        Check.AreEqual(PerformanceStatisticsFactory.CurrentPlatform, info.Platform, "Platform");
                        Check.AreEqual(Environment.ProcessorCount, info.LogicalProcessors, "LogicalProcessors");
                        Check.IsTrue(info.TotalMemoryBytes > 0, "TotalMemoryBytes should be positive");
                        Check.IsTrue(info.BootTimeUtc.HasValue && info.BootTimeUtc.Value < DateTime.UtcNow, "BootTimeUtc should be in the past");
                        Check.IsTrue(ReferenceEquals(info, stats.SystemInfo), "SystemInfo is computed once");
                    }
                }),

                Case(Id, "AllProcessesIncludesCurrent", "GetAllProcesses includes the current process with full metadata", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        IProcessCounters me = FindSelf(stats.GetAllProcesses());
                        Check.IsNotEmpty(me.ExecutablePath, "ExecutablePath");
                        Check.IsTrue(System.IO.File.Exists(me.ExecutablePath), "ExecutablePath should exist: " + me.ExecutablePath);
                        Check.IsNotEmpty(me.CommandLine, "CommandLine");
                        Check.IsNotEmpty(me.UserName, "UserName");
                        Check.IsTrue(me.ParentProcessId > 0, "ParentProcessId should be positive");
                        Check.IsTrue(me.StartTimeUtc.HasValue && me.StartTimeUtc.Value <= DateTime.UtcNow.AddSeconds(5), "StartTimeUtc");
                        Check.AreNotEqual(ProcessStateEnum.Unknown, me.State, "State should be known for the current process");
                        Check.IsTrue(me.Priority.HasValue, "Priority should be known");
                        Check.IsTrue(me.TotalProcessorTime > TimeSpan.Zero, "TotalProcessorTime should be positive");
                        Check.IsTrue(me.DiskReadBytesTotal.HasValue && me.DiskWriteBytesTotal.HasValue, "Disk I/O totals for the current process");
                        Check.IsTrue(me.DiskReadBytesPerSecond >= 0 && me.DiskWriteBytesPerSecond >= 0, "Disk I/O rates for the current process");
                    }
                }),

                Case(Id, "AllProcessesReusedAndOrdered", "GetAllProcesses reuses counters per PID and orders by PID", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        IReadOnlyList<IProcessCounters> first = stats.GetAllProcesses();
                        IReadOnlyList<IProcessCounters> second = stats.GetAllProcesses();
                        Check.IsTrue(first.Count > 1, "Should see more than one process");
                        Check.IsTrue(ReferenceEquals(FindSelf(first), FindSelf(second)), "Same instance for a running process");
                        for (int i = 1; i < second.Count; i++)
                            Check.IsTrue((second[i - 1].ProcessId ?? 0) <= (second[i].ProcessId ?? 0), "Ordered by PID");
                    }
                }),

                Case(Id, "PerCoreCpu", "Per-core CPU has one 0-100 value per logical processor", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        IReadOnlyList<double> cores = stats.System.PerCoreCpuUtilizationPercent;
                        Check.AreEqual(Environment.ProcessorCount, cores.Count, "One value per logical processor");
                        foreach (double c in cores) Check.InRange(c, 0, 100, "Per-core value out of range");
                    }
                }),

                Case(Id, "MemoryTotals", "Memory total, used, and available are consistent", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        double? total = stats.System.MemoryTotalMegabytes;
                        double? used = stats.System.MemoryUsedMegabytes;
                        double? free = stats.System.MemoryFreeMegabytes;
                        Check.IsTrue(total > 0 && used >= 0 && free > 0, "Totals should be available");
                        Check.IsTrue(Math.Abs(total.Value - used.Value - free.Value) <= total.Value * 0.05 + 2, "Used + available should be about total");
                    }
                }),

                Case(Id, "SwapConsistent", "Swap used does not exceed swap total", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        double? total = stats.System.SwapTotalMegabytes;
                        double? used = stats.System.SwapUsedMegabytes;
                        Check.AreEqual(total.HasValue, used.HasValue, "Swap values come together");
                        if (total.HasValue) Check.IsTrue(used.Value <= total.Value + 1, "Swap used <= total");
                    }
                }),

                Case(Id, "DiskBytesAvailable", "Disk read and write bytes per second are available and non-negative", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        double? read = stats.System.TotalDiskReadBytesPerSecond;
                        double? write = stats.System.TotalDiskWriteBytesPerSecond;
                        Check.AreEqual(read.HasValue, write.HasValue, "Read and write bytes come from one sample");
                        if (read.HasValue) Check.IsTrue(read.Value >= 0 && write.Value >= 0, "Non-negative");
                    }
                }),

                Case(Id, "NetworkTotalsMatchInterfaces", "Network totals equal the sum of interfaces", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        IReadOnlyList<NetworkInterfaceCounters> interfaces = stats.System.NetworkInterfaces;
                        double? rx = stats.System.NetworkReceivedBytesPerSecond;
                        double? tx = stats.System.NetworkSentBytesPerSecond;
                        Check.IsTrue(rx.HasValue && tx.HasValue, "Network totals should be available");
                        Check.IsTrue(Math.Abs(rx.Value - interfaces.Sum(i => i.ReceivedBytesPerSecond)) < 1, "Rx total = sum of interfaces");
                        Check.IsTrue(Math.Abs(tx.Value - interfaces.Sum(i => i.SentBytesPerSecond)) < 1, "Tx total = sum of interfaces");
                        foreach (NetworkInterfaceCounters ni in interfaces) Check.IsNotEmpty(ni.Name, "Interface name");
                    }
                }),

                Case(Id, "VolumesMatchTotals", "Volumes sum to the disk space totals", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        IReadOnlyList<VolumeInfo> volumes = stats.System.Volumes;
                        double? size = stats.System.TotalDiskSizeMegabytes;
                        if (!size.HasValue) return;
                        Check.IsTrue(volumes.Count > 0, "At least one volume");
                        double sum = volumes.Sum(v => v.TotalBytes) / (1024.0 * 1024.0);
                        Check.IsTrue(Math.Abs(sum - size.Value) <= 1, "Volume sizes sum to TotalDiskSizeMegabytes");
                        foreach (VolumeInfo v in volumes) Check.IsTrue(v.UsedBytes >= 0 && v.FreeBytes >= 0, "Volume numbers non-negative");
                    }
                }),

                Case(Id, "LoadAveragesByPlatform", "Load averages exist on Linux and macOS and are null on Windows", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        if (IsWindows)
                        {
                            Check.IsNull(stats.System.LoadAverage1, "No load average on Windows");
                            return;
                        }

                        Check.IsTrue(stats.System.LoadAverage1 >= 0 && stats.System.LoadAverage5 >= 0 && stats.System.LoadAverage15 >= 0, "Load averages");
                    }
                }),

                Case(Id, "ActionsOnMissingProcess", "Process actions on a missing PID report NotFound", () =>
                {
                    int missing = FindUnusedPid();
                    Check.AreEqual(ProcessActionResultEnum.NotFound, ProcessActions.Kill(missing), "Kill");
                    Check.AreEqual(ProcessActionResultEnum.NotFound, ProcessActions.SetPriority(missing, ProcessPriorityEnum.BelowNormal), "SetPriority");
                    Check.AreEqual(ProcessActionResultEnum.NotFound, ProcessActions.Suspend(missing), "Suspend");
                }),

                Case(Id, "ActionsLifecycle", "Suspend, resume, lower priority, and kill a child process", () =>
                {
                    using (Process child = StartSleeper())
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        try
                        {
                            Check.AreEqual(ProcessActionResultEnum.Success, ProcessActions.Suspend(child.Id), "Suspend");
                            Check.AreEqual(ProcessStateEnum.Stopped, WaitForState(stats, child.Id, ProcessStateEnum.Stopped), "State after suspend");

                            Check.AreEqual(ProcessActionResultEnum.Success, ProcessActions.Resume(child.Id), "Resume");
                            Check.AreNotEqual(ProcessStateEnum.Stopped, WaitForState(stats, child.Id, ProcessStateEnum.Sleeping, ProcessStateEnum.Running), "State after resume");

                            Check.AreEqual(ProcessActionResultEnum.Success, ProcessActions.SetPriority(child.Id, ProcessPriorityEnum.BelowNormal), "SetPriority");
                            IProcessCounters counters = stats.GetAllProcesses().First(p => p.ProcessId == child.Id);
                            Check.AreEqual(ProcessPriorityEnum.BelowNormal, counters.Priority, "Priority after SetPriority");

                            Check.AreEqual(ProcessActionResultEnum.Success, ProcessActions.Kill(child.Id), "Kill");
                            Check.IsTrue(child.WaitForExit(10000), "Child should exit after Kill");
                        }
                        finally
                        {
                            try { if (!child.HasExited) child.Kill(); } catch { }
                        }
                    }
                }),
            };

            return new TestSuiteDescriptor(Id, "Task Manager Metrics and Actions", cases);
        }

        private static IProcessCounters FindSelf(IReadOnlyList<IProcessCounters> processes)
        {
            int pid;
            using (Process current = Process.GetCurrentProcess()) pid = current.Id;
            IProcessCounters me = processes.FirstOrDefault(p => p.ProcessId == pid);
            if (me == null) throw new InvalidOperationException("Current process " + pid + " not in GetAllProcesses()");
            return me;
        }

        private static Process StartSleeper()
        {
            ProcessStartInfo psi = IsWindows
                ? new ProcessStartInfo("ping", "-n 120 127.0.0.1")
                : new ProcessStartInfo("/bin/sleep", "120");
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            Process p = Process.Start(psi);
            Thread.Sleep(300);
            return p;
        }

        private static ProcessStateEnum WaitForState(IPerformanceStatistics stats, int pid, params ProcessStateEnum[] wanted)
        {
            ProcessStateEnum state = ProcessStateEnum.Unknown;
            for (int i = 0; i < 40; i++)
            {
                IProcessCounters counters = stats.GetAllProcesses().FirstOrDefault(p => p.ProcessId == pid);
                state = counters != null ? counters.State : ProcessStateEnum.Unknown;
                if (wanted.Contains(state)) return state;
                Thread.Sleep(50);
            }

            return state;
        }

        private static int FindUnusedPid()
        {
            HashSet<int> used = new HashSet<int>(Process.GetProcesses().Select(p => p.Id));
            int pid = 999999;
            while (used.Contains(pid)) pid--;
            return pid;
        }
    }
}
