using System;
using System.Collections.Generic;
using System.Threading;
using PerformanceStatistics;
using Touchstone.Core;
using static Test.Shared.TestSupport;

namespace Test.Shared.Suites
{
    /// <summary>
    /// Cross-platform tests for <see cref="ISystemCounters"/>. Every metric is nullable
    /// (unavailable on a given platform), so each assertion accepts null or a valid range.
    /// </summary>
    public static class SystemCountersSuite
    {
        private const string Id = "SystemCounters";

        /// <summary>
        /// Build the system counters test suite.
        /// </summary>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Case(Id, "SystemNotNull", "System counters object is not null", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        Check.IsNotNull(stats.System, "System should not be null");
                    }
                }),

                Case(Id, "CpuInRangeOrNull", "CpuUtilizationPercent is null or within 0-100", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        double? cpu = stats.System.CpuUtilizationPercent;
                        if (cpu.HasValue) Check.InRange(cpu.Value, 0, 100, "CPU utilization out of range");
                    }
                }),

                Case(Id, "CpuSecondSampleValid", "CpuUtilizationPercent stays valid across two samples", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        double? cpu1 = stats.System.CpuUtilizationPercent;
                        Thread.Sleep(250);
                        double? cpu2 = stats.System.CpuUtilizationPercent;
                        if (cpu1.HasValue) Check.InRange(cpu1.Value, 0, 100, "First CPU sample out of range");
                        if (cpu2.HasValue) Check.InRange(cpu2.Value, 0, 100, "Second CPU sample out of range");
                    }
                }),

                Case(Id, "MemoryFreePositiveOrNull", "MemoryFreeMegabytes is null or greater than zero", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        double? mem = stats.System.MemoryFreeMegabytes;
                        if (mem.HasValue) Check.IsTrue(mem.Value > 0, "Free memory should be positive: " + mem.Value);
                    }
                }),

                Case(Id, "DiskReadOpsNonNegativeOrNull", "TotalDiskReadOperations is null or non-negative", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        int? ops = stats.System.TotalDiskReadOperations;
                        if (ops.HasValue) Check.IsTrue(ops.Value >= 0, "Read ops should be non-negative: " + ops.Value);
                    }
                }),

                Case(Id, "DiskWriteOpsNonNegativeOrNull", "TotalDiskWriteOperations is null or non-negative", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        int? ops = stats.System.TotalDiskWriteOperations;
                        if (ops.HasValue) Check.IsTrue(ops.Value >= 0, "Write ops should be non-negative: " + ops.Value);
                    }
                }),

                Case(Id, "DiskReadQueueNonNegativeOrNull", "TotalDiskReadQueue is null or non-negative", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        int? q = stats.System.TotalDiskReadQueue;
                        if (q.HasValue) Check.IsTrue(q.Value >= 0, "Read queue should be non-negative: " + q.Value);
                    }
                }),

                Case(Id, "DiskWriteQueueNonNegativeOrNull", "TotalDiskWriteQueue is null or non-negative", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        int? q = stats.System.TotalDiskWriteQueue;
                        if (q.HasValue) Check.IsTrue(q.Value >= 0, "Write queue should be non-negative: " + q.Value);
                    }
                }),

                Case(Id, "DiskFreePercentInRangeOrNull", "TotalDiskFreePercent is null or within 0-100", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        double? pct = stats.System.TotalDiskFreePercent;
                        if (pct.HasValue) Check.InRange(pct.Value, 0, 100, "Disk free percent out of range");
                    }
                }),

                Case(Id, "DiskFreeMegabytesNonNegativeOrNull", "TotalDiskFreeMegabytes is null or non-negative", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        double? mb = stats.System.TotalDiskFreeMegabytes;
                        if (mb.HasValue) Check.IsTrue(mb.Value >= 0, "Disk free MB should be non-negative: " + mb.Value);
                    }
                }),

                Case(Id, "DiskSizePositiveOrNull", "TotalDiskSizeMegabytes is null or positive", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        double? mb = stats.System.TotalDiskSizeMegabytes;
                        if (mb.HasValue) Check.IsTrue(mb.Value > 0, "Disk size MB should be positive: " + mb.Value);
                    }
                }),

                Case(Id, "DiskUsedNonNegativeOrNull", "TotalDiskUsedMegabytes is null or non-negative", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        double? mb = stats.System.TotalDiskUsedMegabytes;
                        if (mb.HasValue) Check.IsTrue(mb.Value >= 0, "Disk used MB should be non-negative: " + mb.Value);
                    }
                }),

                Case(Id, "DiskUsedPlusFreeEqualsTotal", "Used + Free is consistent with Total disk size", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        double? free = stats.System.TotalDiskFreeMegabytes;
                        double? used = stats.System.TotalDiskUsedMegabytes;
                        double? total = stats.System.TotalDiskSizeMegabytes;
                        if (!free.HasValue || !used.HasValue || !total.HasValue) return;

                        double calculated = free.Value + used.Value;
                        double tolerance = Math.Max(1.0, total.Value * 0.01);
                        Check.IsTrue(Math.Abs(calculated - total.Value) <= tolerance,
                            "Used(" + used.Value + ") + Free(" + free.Value + ") should approx equal Total(" + total.Value + ")");
                    }
                }),

                Case(Id, "DiskFreeNotGreaterThanTotal", "Free disk space does not exceed total disk size", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        double? free = stats.System.TotalDiskFreeMegabytes;
                        double? total = stats.System.TotalDiskSizeMegabytes;
                        if (!free.HasValue || !total.HasValue) return;
                        double tolerance = Math.Max(1.0, total.Value * 0.01);
                        Check.IsTrue(free.Value <= total.Value + tolerance,
                            "Free(" + free.Value + ") should not exceed Total(" + total.Value + ")");
                    }
                }),

                Case(Id, "DiskUsedNotGreaterThanTotal", "Used disk space does not exceed total disk size", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        double? used = stats.System.TotalDiskUsedMegabytes;
                        double? total = stats.System.TotalDiskSizeMegabytes;
                        if (!used.HasValue || !total.HasValue) return;
                        double tolerance = Math.Max(1.0, total.Value * 0.01);
                        Check.IsTrue(used.Value <= total.Value + tolerance,
                            "Used(" + used.Value + ") should not exceed Total(" + total.Value + ")");
                    }
                }),

                Case(Id, "SystemToStringNotEmpty", "System counters ToString() returns non-empty text", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        string s = stats.System.ToString();
                        Check.IsNotEmpty(s, "System ToString should be non-empty");
                    }
                }),

                Case(Id, "RepeatedAccessStable", "Repeated system counter access does not throw", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            _ = stats.System.CpuUtilizationPercent;
                            _ = stats.System.MemoryFreeMegabytes;
                            _ = stats.System.TotalDiskFreePercent;
                        }
                    }
                }),
            };

            return new TestSuiteDescriptor(Id, "System Counters (Cross-Platform)", cases);
        }
    }
}
