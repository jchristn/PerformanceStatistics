using System;
using System.Collections.Generic;
using PerformanceStatistics;
using Touchstone.Core;
using static Test.Shared.TestSupport;

namespace Test.Shared.Suites
{
    /// <summary>
    /// Tests for <see cref="IDisposable"/> semantics on
    /// <see cref="IPerformanceStatistics"/> and <see cref="IProcessCounters"/>.
    /// </summary>
    public static class DisposalSuite
    {
        private const string Id = "Disposal";

        /// <summary>
        /// Build the disposal test suite.
        /// </summary>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Case(Id, "ImplementsIDisposable", "IPerformanceStatistics implements IDisposable", () =>
                {
                    IPerformanceStatistics stats = PerformanceStatisticsFactory.Create();
                    try
                    {
                        Check.IsTrue(stats is IDisposable, "Should implement IDisposable");
                    }
                    finally
                    {
                        stats.Dispose();
                    }
                }),

                Case(Id, "DisposeIsIdempotent", "Dispose can be called multiple times without error", () =>
                {
                    IPerformanceStatistics stats = PerformanceStatisticsFactory.Create();
                    stats.Dispose();
                    Check.DoesNotThrow(() => stats.Dispose(), "Second Dispose should not throw");
                    Check.DoesNotThrow(() => stats.Dispose(), "Third Dispose should not throw");
                }),

                Case(Id, "UsingStatementWorks", "using statement disposes cleanly after property access", () =>
                {
                    bool completed = false;
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        _ = stats.System.CpuUtilizationPercent;
                        completed = true;
                    }
                    Check.IsTrue(completed, "using block should complete and dispose");
                }),

                Case(Id, "MonitoredProcessesRefreshDisposes", "Refreshing MonitoredProcesses disposes prior handles without error", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        stats.MonitoredProcessNames.Add(CurrentProcessName);
                        for (int i = 0; i < 3; i++)
                        {
                            Dictionary<string, List<IProcessCounters>> procs = stats.MonitoredProcesses;
                            Check.IsNotNull(procs, "Refresh should return a dictionary");
                        }
                    }
                }),

                Case(Id, "ProcessCounterDisposeIdempotent", "IProcessCounters Dispose is idempotent", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        stats.MonitoredProcessNames.Add(CurrentProcessName);
                        Dictionary<string, List<IProcessCounters>> procs = stats.MonitoredProcesses;
                        if (procs[CurrentProcessName].Count == 0) return;
                        IProcessCounters proc = procs[CurrentProcessName][0];
                        Check.DoesNotThrow(() => { proc.Dispose(); proc.Dispose(); }, "Double dispose should not throw");
                    }
                }),
            };

            return new TestSuiteDescriptor(Id, "Disposal & Lifecycle", cases);
        }
    }
}
