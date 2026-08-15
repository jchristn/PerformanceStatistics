using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PerformanceStatistics;
using Touchstone.Core;
using static Test.Shared.TestSupport;

namespace Test.Shared.Suites
{
    /// <summary>
    /// Edge case, robustness, and concurrency tests spanning the whole surface area.
    /// </summary>
    public static class EdgeCaseSuite
    {
        private const string Id = "EdgeCases";

        /// <summary>
        /// Build the edge case test suite.
        /// </summary>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Case(Id, "EmptyMonitoredListWorks", "A freshly assigned empty monitored list yields no processes", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        stats.MonitoredProcessNames = new List<string>();
                        Check.AreEqual(0, stats.MonitoredProcesses.Count, "No processes should be reported");
                    }
                }),

                Case(Id, "InstanceToStringNotEmpty", "IPerformanceStatistics ToString() returns non-empty text", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        Check.IsNotEmpty(stats.ToString(), "ToString should be non-empty");
                    }
                }),

                Case(Id, "RapidSuccessiveAccess", "Rapid successive property access does not throw", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            _ = stats.System.CpuUtilizationPercent;
                            _ = stats.System.MemoryFreeMegabytes;
                            _ = stats.ActiveTcpConnections;
                        }
                    }
                }),

                Case(Id, "WhitespaceProcessNameEmptyList", "Whitespace process name yields an empty list (negative)", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        stats.MonitoredProcessNames.Add("   ");
                        Dictionary<string, List<IProcessCounters>> procs = stats.MonitoredProcesses;
                        Check.IsTrue(procs.ContainsKey("   "), "Key should be present");
                        Check.AreEqual(0, procs["   "].Count, "Whitespace name should match no processes");
                    }
                }),

                Case(Id, "ProcessNameWithExtensionEmptyList", "Process name with .exe extension yields an empty list (negative)", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        // GetProcessesByName expects the base name without extension, so ".exe" should not match.
                        string bogus = NonExistentProcessName + ".exe";
                        stats.MonitoredProcessNames.Add(bogus);
                        Check.AreEqual(0, stats.MonitoredProcesses[bogus].Count, "Extension-qualified bogus name should not match");
                    }
                }),

                Case(Id, "ReassignMonitoredNamesReflected", "Reassigning MonitoredProcessNames replaces the monitored set", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        stats.MonitoredProcessNames = new List<string> { "a", "b", "c" };
                        Check.AreEqual(3, stats.MonitoredProcessNames.Count, "Should hold three names");
                        stats.MonitoredProcessNames = new List<string> { "x" };
                        Check.AreEqual(1, stats.MonitoredProcessNames.Count, "Should hold one name after reassign");
                        Check.AreEqual("x", stats.MonitoredProcessNames[0], "Should be the new name");
                    }
                }),

                CaseAsync(Id, "ConcurrentAccessDoesNotThrow", "Concurrent access to counters is thread-safe", async ct =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        stats.MonitoredProcessNames.Add(CurrentProcessName);
                        List<Task> tasks = new List<Task>();
                        for (int i = 0; i < 5; i++)
                        {
                            tasks.Add(Task.Run(() =>
                            {
                                for (int j = 0; j < 10; j++)
                                {
                                    _ = stats.MonitoredProcesses;
                                    _ = stats.System.CpuUtilizationPercent;
                                }
                            }, ct));
                        }
                        await Task.WhenAll(tasks);
                    }
                }),
            };

            return new TestSuiteDescriptor(Id, "Edge Cases & Concurrency", cases);
        }
    }
}
