using System;
using System.Collections.Generic;
using System.Threading;
using PerformanceStatistics;
using Touchstone.Core;
using static Test.Shared.TestSupport;

namespace Test.Shared.Suites
{
    /// <summary>
    /// Tests for process monitoring via <see cref="IPerformanceStatistics.MonitoredProcesses"/>
    /// and the resulting <see cref="IProcessCounters"/> instances. Uses the current process
    /// as a guaranteed-present target and a bogus name for negative cases.
    /// </summary>
    public static class ProcessMonitoringSuite
    {
        private const string Id = "ProcessMonitoring";

        // Helper: create stats monitoring the current process and return the first counter.
        private static IProcessCounters FirstCurrentProcessCounter(IPerformanceStatistics stats)
        {
            string name = CurrentProcessName;
            stats.MonitoredProcessNames.Add(name);
            Dictionary<string, List<IProcessCounters>> procs = stats.MonitoredProcesses;
            Check.IsTrue(procs.ContainsKey(name), "Result should contain the current process key");
            Check.IsTrue(procs[name].Count > 0, "Current process should have at least one instance");
            return procs[name][0];
        }

        /// <summary>
        /// Build the process monitoring test suite.
        /// </summary>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Case(Id, "EmptyWhenNoneMonitored", "MonitoredProcesses is empty when no names are set", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        stats.MonitoredProcessNames.Clear();
                        Dictionary<string, List<IProcessCounters>> procs = stats.MonitoredProcesses;
                        Check.IsNotNull(procs, "MonitoredProcesses should not be null");
                        Check.AreEqual(0, procs.Count, "MonitoredProcesses should be empty");
                    }
                }),

                Case(Id, "FindsCurrentProcess", "MonitoredProcesses finds the current process", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        string name = CurrentProcessName;
                        stats.MonitoredProcessNames.Add(name);
                        Dictionary<string, List<IProcessCounters>> procs = stats.MonitoredProcesses;
                        Check.IsTrue(procs.ContainsKey(name), "Should contain current process key");
                        Check.IsTrue(procs[name].Count > 0, "Should find at least one current process instance");
                    }
                }),

                Case(Id, "NonExistentProcessEmptyList", "Non-existent process yields an empty list (negative)", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        stats.MonitoredProcessNames.Add(NonExistentProcessName);
                        Dictionary<string, List<IProcessCounters>> procs = stats.MonitoredProcesses;
                        Check.IsTrue(procs.ContainsKey(NonExistentProcessName), "Key should still be present");
                        Check.AreEqual(0, procs[NonExistentProcessName].Count, "List should be empty for missing process");
                    }
                }),

                Case(Id, "ProcessIdPositive", "ProcessId is present and positive", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        IProcessCounters proc = FirstCurrentProcessCounter(stats);
                        Check.IsTrue(proc.ProcessId.HasValue, "ProcessId should have a value");
                        Check.IsTrue(proc.ProcessId.Value > 0, "ProcessId should be positive: " + proc.ProcessId.Value);
                    }
                }),

                Case(Id, "ProcessNameMatches", "ProcessName matches the monitored name", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        IProcessCounters proc = FirstCurrentProcessCounter(stats);
                        Check.AreEqual(CurrentProcessName, proc.ProcessName, "ProcessName should match");
                    }
                }),

                Case(Id, "ProcessTitleNotNull", "ProcessTitle is never null", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        IProcessCounters proc = FirstCurrentProcessCounter(stats);
                        Check.IsNotNull(proc.ProcessTitle, "ProcessTitle should not be null (may be empty)");
                    }
                }),

                Case(Id, "MachineNameNotEmpty", "MachineName is non-empty", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        IProcessCounters proc = FirstCurrentProcessCounter(stats);
                        Check.IsNotEmpty(proc.MachineName, "MachineName should be non-empty");
                    }
                }),

                Case(Id, "ProcessCpuValidOrNull", "Process CpuUtilizationPercent is null or non-negative", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        IProcessCounters proc = FirstCurrentProcessCounter(stats);
                        Thread.Sleep(120);
                        double? cpu = proc.CpuUtilizationPercent;
                        if (cpu.HasValue) Check.IsTrue(cpu.Value >= 0, "Process CPU should be non-negative: " + cpu.Value);
                    }
                }),

                Case(Id, "ThreadCountPositive", "ThreadCount is present and positive", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        IProcessCounters proc = FirstCurrentProcessCounter(stats);
                        Check.IsTrue(proc.ThreadCount.HasValue, "ThreadCount should have a value");
                        Check.IsTrue(proc.ThreadCount.Value > 0, "ThreadCount should be positive: " + proc.ThreadCount.Value);
                    }
                }),

                Case(Id, "HandleCountNonNegativeOrNull", "HandleCount is null or non-negative", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        IProcessCounters proc = FirstCurrentProcessCounter(stats);
                        int? h = proc.HandleCount;
                        if (h.HasValue) Check.IsTrue(h.Value >= 0, "HandleCount should be non-negative: " + h.Value);
                    }
                }),

                Case(Id, "WorkingSetPositive", "WorkingSetMemory is present and positive", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        IProcessCounters proc = FirstCurrentProcessCounter(stats);
                        Check.IsTrue(proc.WorkingSetMemory.HasValue, "WorkingSetMemory should have a value");
                        Check.IsTrue(proc.WorkingSetMemory.Value > 0, "WorkingSetMemory should be positive");
                    }
                }),

                Case(Id, "VirtualMemoryPositiveOrNull", "VirtualMemory is null or positive", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        IProcessCounters proc = FirstCurrentProcessCounter(stats);
                        long? v = proc.VirtualMemory;
                        if (v.HasValue) Check.IsTrue(v.Value > 0, "VirtualMemory should be positive: " + v.Value);
                    }
                }),

                Case(Id, "PrivateMemoryNonNegativeOrNull", "PrivateMemory is null or non-negative", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        IProcessCounters proc = FirstCurrentProcessCounter(stats);
                        long? p = proc.PrivateMemory;
                        if (p.HasValue) Check.IsTrue(p.Value >= 0, "PrivateMemory should be non-negative: " + p.Value);
                    }
                }),

                Case(Id, "PeakWorkingSetNonNegativeOrNull", "PeakWorkingSetMemory is null or non-negative", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        IProcessCounters proc = FirstCurrentProcessCounter(stats);
                        long? p = proc.PeakWorkingSetMemory;
                        if (p.HasValue) Check.IsTrue(p.Value >= 0, "PeakWorkingSetMemory should be non-negative: " + p.Value);
                    }
                }),

                Case(Id, "PeakVirtualNonNegativeOrNull", "PeakVirtualSystemMemory is null or non-negative", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        IProcessCounters proc = FirstCurrentProcessCounter(stats);
                        long? p = proc.PeakVirtualSystemMemory;
                        if (p.HasValue) Check.IsTrue(p.Value >= 0, "PeakVirtualSystemMemory should be non-negative: " + p.Value);
                    }
                }),

                Case(Id, "WorkingSetNotAbovePeak", "WorkingSet does not exceed Peak working set (when tracked)", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        IProcessCounters proc = FirstCurrentProcessCounter(stats);
                        long? working = proc.WorkingSetMemory;
                        long? peak = proc.PeakWorkingSetMemory;
                        if (!working.HasValue || !peak.HasValue) return;
                        if (peak.Value == 0) return; // Not tracked on some platforms (macOS)
                        Check.IsTrue(working.Value <= peak.Value,
                            "Working(" + working.Value + ") should be <= Peak(" + peak.Value + ")");
                    }
                }),

                Case(Id, "ProcessToStringNotEmpty", "Process counters ToString() returns non-empty text", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        IProcessCounters proc = FirstCurrentProcessCounter(stats);
                        Check.IsNotEmpty(proc.ToString(), "Process ToString should be non-empty");
                    }
                }),

                Case(Id, "RefreshDisposesOldHandles", "Repeated MonitoredProcesses access refreshes without error", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        stats.MonitoredProcessNames.Add(CurrentProcessName);
                        _ = stats.MonitoredProcesses;
                        _ = stats.MonitoredProcesses;
                        _ = stats.MonitoredProcesses;
                    }
                }),

                Case(Id, "SetMonitoredNamesNullBecomesEmpty", "Setting MonitoredProcessNames to null yields empty list", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        stats.MonitoredProcessNames = null;
                        Check.IsNotNull(stats.MonitoredProcessNames, "Should not be null after null assignment");
                        Check.AreEqual(0, stats.MonitoredProcessNames.Count, "Should be empty after null assignment");
                    }
                }),

                Case(Id, "MultipleDistinctNames", "Monitoring multiple distinct names returns all keys", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        stats.MonitoredProcessNames.Add(CurrentProcessName);
                        stats.MonitoredProcessNames.Add(NonExistentProcessName);
                        Dictionary<string, List<IProcessCounters>> procs = stats.MonitoredProcesses;
                        Check.AreEqual(2, procs.Count, "Should have a key per distinct monitored name");
                        Check.IsTrue(procs.ContainsKey(CurrentProcessName), "Should contain the current process");
                        Check.IsTrue(procs.ContainsKey(NonExistentProcessName), "Should contain the bogus process key");
                    }
                }),
            };

            return new TestSuiteDescriptor(Id, "Process Monitoring", cases);
        }
    }
}
