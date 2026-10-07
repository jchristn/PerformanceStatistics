using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using PerformanceStatistics;
using PerformanceStatistics.Linux;
using PerformanceStatistics.Mac;
using PerformanceStatistics.Windows;
using Touchstone.Core;
using static Test.Shared.TestSupport;

namespace Test.Shared.Suites
{
    /// <summary>
    /// Behavior that must be identical on Windows, Linux, and macOS, and regression tests for
    /// cross-platform defects fixed in v2.1.0.
    /// </summary>
    public static class ConsistencySuite
    {
        private const string Id = "Consistency";

        /// <summary>
        /// Build the consistency test suite.
        /// </summary>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Case(Id, "ProcessCountersReusedAcrossRefreshes", "MonitoredProcesses returns the same live counter instance for a running process", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        stats.MonitoredProcessNames.Add(CurrentProcessName);
                        IProcessCounters first = FindCurrentProcess(stats);
                        IProcessCounters second = FindCurrentProcess(stats);
                        Check.IsTrue(ReferenceEquals(first, second), "A running process should keep its counter instance");
                        Check.IsNotNull(first.ProcessId, "A counter from an earlier access must not be disposed");
                    }
                }),

                Case(Id, "ProcessCpuMeasuredBetweenRefreshes", "Process CPU is non-zero when the process burns CPU between two accesses", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        stats.MonitoredProcessNames.Add(CurrentProcessName);
                        FindCurrentProcess(stats);
                        BurnCpu(TimeSpan.FromMilliseconds(400), 1);
                        double? cpu = FindCurrentProcess(stats).CpuUtilizationPercent;
                        Check.IsTrue(cpu.HasValue, "Process CPU should be available for the current process");
                        Check.IsTrue(cpu.Value > 0, "Process CPU should be measured across accesses, got " + cpu.Value);
                        Check.InRange(cpu.Value, 0, 100, "Process CPU is normalized to 0-100 of machine capacity");
                    }
                }),

                Case(Id, "SystemCpuFirstReadIsMeasured", "The first system CPU read reflects load instead of returning a placeholder 0", () =>
                {
                    using (CancellationTokenSource cts = new CancellationTokenSource())
                    {
                        List<Thread> burners = new List<Thread>();
                        for (int i = 0; i < Math.Max(1, Environment.ProcessorCount / 2); i++)
                        {
                            Thread burner = new Thread(() => { while (!cts.IsCancellationRequested) { } });
                            burner.IsBackground = true;
                            burner.Start();
                            burners.Add(burner);
                        }

                        try
                        {
                            Thread.Sleep(50);
                            using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                            {
                                double? cpu = stats.System.CpuUtilizationPercent;
                                Check.IsTrue(cpu.HasValue, "System CPU should be available");
                                Check.IsTrue(cpu.Value > 0, "A busy core should register on the first read");
                            }
                        }
                        finally
                        {
                            cts.Cancel();
                            foreach (Thread burner in burners) burner.Join();
                        }
                    }
                }),

                Case(Id, "DiskReadThenWriteBothAvailable", "Reading disk read ops then write ops back-to-back yields values for both", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        int? reads = stats.System.TotalDiskReadOperations;
                        int? writes = stats.System.TotalDiskWriteOperations;
                        Check.AreEqual(reads.HasValue, writes.HasValue, "Read and write ops come from one sample and must agree on availability");
                        if (reads.HasValue) Check.IsTrue(reads.Value >= 0 && writes.Value >= 0, "Rates must be non-negative");
                    }
                }),

                Case(Id, "RapidCpuReadsDoNotReset", "Back-to-back CPU reads return the cached value rather than resetting to 0", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        BurnCpu(TimeSpan.FromMilliseconds(200), 1);
                        double? first = stats.System.CpuUtilizationPercent;
                        double? second = stats.System.CpuUtilizationPercent;
                        Check.AreEqual(first, second, "A read inside the minimum interval should repeat the last value");
                    }
                }),

                Case(Id, "DiskSizeNotInflated", "Total disk size does not exceed the sum of distinct mounted filesystems", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        double? size = stats.System.TotalDiskSizeMegabytes;
                        if (!size.HasValue) return;
                        double distinctMb = DistinctFilesystemCapacityMegabytes();
                        Check.IsTrue(size.Value <= distinctMb + 1,
                            "Disk size " + size.Value + "MB exceeds distinct filesystem capacity " + distinctMb + "MB (volumes double-counted)");
                    }
                }),

                Case(Id, "MemoryFreeWithinPhysical", "Free memory is positive and not above total physical memory", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        double? free = stats.System.MemoryFreeMegabytes;
                        Check.IsTrue(free.HasValue && free.Value > 0, "Free memory should be positive");
                        double physicalMb = PhysicalMemoryMegabytes();
                        Check.IsTrue(free.Value <= physicalMb + 1,
                            "Free memory " + free.Value + "MB exceeds physical memory " + physicalMb + "MB");
                    }
                }),

                Case(Id, "DuplicateMonitoredNamesTolerated", "Duplicate monitored names do not throw and yield one key", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        stats.MonitoredProcessNames.Add(CurrentProcessName);
                        stats.MonitoredProcessNames.Add(CurrentProcessName);
                        Dictionary<string, List<IProcessCounters>> procs = stats.MonitoredProcesses;
                        Check.AreEqual(1, procs.Count, "Duplicate names should collapse into one key");
                    }
                }),

                Case(Id, "RemovedNameDisposesCounters", "Removing a monitored name disposes its counters", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        stats.MonitoredProcessNames.Add(CurrentProcessName);
                        IProcessCounters proc = FindCurrentProcess(stats);
                        stats.MonitoredProcessNames.Clear();
                        Check.AreEqual(0, stats.MonitoredProcesses.Count, "No names should yield no keys");
                        Check.IsNull(proc.ProcessId, "Counters for a removed name should be disposed");
                    }
                }),

                Case(Id, "DisposedStatisticsAreInert", "After Dispose, system counters return null and no processes are tracked", () =>
                {
                    IPerformanceStatistics stats = PerformanceStatisticsFactory.Create();
                    stats.MonitoredProcessNames.Add(CurrentProcessName);
                    IProcessCounters proc = FindCurrentProcess(stats);
                    stats.Dispose();
                    Check.IsNull(stats.System.CpuUtilizationPercent, "CPU should be null after Dispose");
                    Check.IsNull(stats.System.MemoryFreeMegabytes, "Memory should be null after Dispose");
                    Check.AreEqual(0, stats.MonitoredProcesses.Count, "No processes after Dispose");
                    Check.IsNull(proc.ProcessId, "Tracked process counters are disposed with the instance");
                }),

                Case(Id, "WrongPlatformConstructorsThrow", "System counters for another operating system throw PlatformNotSupportedException", () =>
                {
                    if (!IsWindows) Check.Throws<PlatformNotSupportedException>(() => new WindowsSystemCounters(), "Windows counters off Windows");
                    if (!IsLinux) Check.Throws<PlatformNotSupportedException>(() => new LinuxSystemCounters(), "Linux counters off Linux");
                    if (!IsMac) Check.Throws<PlatformNotSupportedException>(() => new MacSystemCounters(), "macOS counters off macOS");
                    if (!IsWindows) Check.Throws<PlatformNotSupportedException>(() => new WindowsPerformanceStatistics(), "Windows statistics off Windows");
                }),

                Case(Id, "CurrentProcessHandleCountPositive", "HandleCount for the current process is positive on every platform", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        stats.MonitoredProcessNames.Add(CurrentProcessName);
                        int? handles = FindCurrentProcess(stats).HandleCount;
                        Check.IsTrue(handles.HasValue && handles.Value > 0, "Own process handle/descriptor count should be positive, got " + handles);
                    }
                }),

                Case(Id, "ToStringIsCultureInvariant", "ToString output does not depend on the current culture", () =>
                {
                    System.Globalization.CultureInfo original = System.Globalization.CultureInfo.CurrentCulture;
                    try
                    {
                        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                        using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                        {
                            stats.MonitoredProcessNames.Add(CurrentProcessName);
                            FindCurrentProcess(stats);
                            BurnCpu(TimeSpan.FromMilliseconds(150), 1);
                            string text = stats.System.ToString() + FindCurrentProcess(stats).ToString();
                            Check.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(text, @"\d,\d"), "Numbers should be formatted with the invariant culture: " + text);
                        }
                    }
                    finally
                    {
                        System.Globalization.CultureInfo.CurrentCulture = original;
                    }
                }),
            };

            return new TestSuiteDescriptor(Id, "Cross-Platform Consistency", cases);
        }

        private static IProcessCounters FindCurrentProcess(IPerformanceStatistics stats)
        {
            int pid;
            using (Process current = Process.GetCurrentProcess()) pid = current.Id;

            foreach (IProcessCounters counters in stats.MonitoredProcesses[CurrentProcessName])
            {
                if (counters.ProcessId == pid) return counters;
            }

            throw new InvalidOperationException("Current process " + pid + " was not found under '" + CurrentProcessName + "'");
        }

        private static void BurnCpu(TimeSpan duration, int threads)
        {
            List<Thread> workers = new List<Thread>();
            for (int i = 0; i < threads; i++)
            {
                Thread t = new Thread(() =>
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    while (sw.Elapsed < duration) { }
                });
                t.Start();
                workers.Add(t);
            }

            foreach (Thread t in workers) t.Join();
        }

        private static double PhysicalMemoryMegabytes()
        {
            if (IsLinux)
            {
                foreach (string line in File.ReadLines("/proc/meminfo"))
                {
                    if (!line.StartsWith("MemTotal:", StringComparison.Ordinal)) continue;
                    string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    return Double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) / 1024.0;
                }
            }

            return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024.0 * 1024.0);
        }

        private static double DistinctFilesystemCapacityMegabytes()
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            double total = 0;
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady || drive.TotalSize <= 0) continue;
                    string key = drive.DriveFormat + "|" + drive.TotalSize + "|" + drive.AvailableFreeSpace;
                    if (!seen.Add(key)) continue;
                    total += drive.TotalSize / (1024.0 * 1024.0);
                }
                catch
                {
                }
            }

            return total;
        }
    }
}
