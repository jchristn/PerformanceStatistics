using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using PerformanceStatistics;
using PerformanceStatistics.Windows;
using PerformanceStatistics.Linux;
using PerformanceStatistics.Mac;
using Touchstone.Core;
using static Test.Shared.TestSupport;

namespace Test.Shared.Suites
{
    /// <summary>
    /// Platform-specific tests that exercise the concrete implementation classes directly.
    /// Only the suite matching the current platform is included in the run, so the
    /// direct-instantiation assertions never execute on the wrong OS.
    /// </summary>
    public static class PlatformSpecificSuite
    {
        /// <summary>
        /// Build the platform-specific suite for the current platform, or null if unsupported.
        /// </summary>
        public static TestSuiteDescriptor BuildForCurrentPlatform()
        {
            if (IsWindows) return BuildWindows();
            if (IsLinux) return BuildLinux();
            if (IsMac) return BuildMac();
            return null;
        }

        #region Windows

        private static TestSuiteDescriptor BuildWindows()
        {
            const string Id = "Windows";

            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Case(Id, "DirectInstantiation", "WindowsPerformanceStatistics can be instantiated directly", () =>
                {
                    using (WindowsPerformanceStatistics stats = new WindowsPerformanceStatistics())
                    {
                        Check.IsNotNull(stats, "Instance should not be null");
                        Check.IsTrue(stats.System is WindowsSystemCounters, "System should be WindowsSystemCounters");
                    }
                }),

                Case(Id, "ConstructorWithProcessList", "WindowsPerformanceStatistics carries a supplied process list", () =>
                {
                    using (WindowsPerformanceStatistics stats = new WindowsPerformanceStatistics(new List<string> { "dotnet" }))
                    {
                        Check.AreEqual(1, stats.MonitoredProcessNames.Count, "Should carry the supplied name");
                    }
                }),

                Case(Id, "CpuInRange", "WindowsSystemCounters CpuUtilizationPercent is within 0-100", () =>
                {
                    WindowsSystemCounters sys = new WindowsSystemCounters();
                    double? cpu = sys.CpuUtilizationPercent;
                    Check.IsTrue(cpu.HasValue, "CPU should return a value on Windows");
                    Check.InRange(cpu.Value, 0, 100, "CPU utilization out of range");
                }),

                Case(Id, "MemoryFreePositive", "WindowsSystemCounters MemoryFreeMegabytes is positive", () =>
                {
                    WindowsSystemCounters sys = new WindowsSystemCounters();
                    double? mem = sys.MemoryFreeMegabytes;
                    Check.IsTrue(mem.HasValue && mem.Value > 0, "Free memory should be positive");
                }),

                Case(Id, "DiskMetricsValid", "WindowsSystemCounters disk metrics are valid", () =>
                {
                    WindowsSystemCounters sys = new WindowsSystemCounters();
                    Check.IsTrue(sys.TotalDiskSizeMegabytes > 0, "Disk size should be positive");
                    Check.IsTrue(sys.TotalDiskFreeMegabytes >= 0, "Disk free should be non-negative");
                    Check.IsTrue(sys.TotalDiskUsedMegabytes >= 0, "Disk used should be non-negative");
                }),

                Case(Id, "SystemToStringNotEmpty", "WindowsSystemCounters ToString() is non-empty", () =>
                {
                    Check.IsNotEmpty(new WindowsSystemCounters().ToString(), "ToString should be non-empty");
                }),

                Case(Id, "ProcessCounterNullThrows", "WindowsProcessCounters(null) throws ArgumentNullException (negative)", () =>
                {
                    Check.Throws<ArgumentNullException>(() => new WindowsProcessCounters(null),
                        "Null process should throw ArgumentNullException");
                }),

                Case(Id, "ProcessWindowsMemoryFields", "WindowsProcessCounters exposes Windows-specific memory fields", () =>
                {
                    using (Process p = Process.GetCurrentProcess())
                    using (WindowsProcessCounters proc = new WindowsProcessCounters(p))
                    {
                        Check.IsTrue(proc.NonPagedSystemMemory.HasValue, "NonPagedSystemMemory should be present on Windows");
                        Check.IsTrue(proc.PagedSystemMemory.HasValue, "PagedSystemMemory should be present on Windows");
                        Check.IsTrue(proc.PeakPagedSystemMemory.HasValue, "PeakPagedSystemMemory should be present on Windows");
                        Check.IsTrue(proc.WorkingSetMemory.HasValue && proc.WorkingSetMemory.Value > 0, "WorkingSet should be positive");
                    }
                }),
            };

            return new TestSuiteDescriptor(Id, "Windows-Specific Implementation", cases);
        }

        #endregion

        #region Linux

        private static TestSuiteDescriptor BuildLinux()
        {
            const string Id = "Linux";

            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Case(Id, "DirectInstantiation", "LinuxPerformanceStatistics can be instantiated directly", () =>
                {
                    using (LinuxPerformanceStatistics stats = new LinuxPerformanceStatistics())
                    {
                        Check.IsNotNull(stats, "Instance should not be null");
                        Check.IsTrue(stats.System is LinuxSystemCounters, "System should be LinuxSystemCounters");
                    }
                }),

                Case(Id, "CpuInRange", "LinuxSystemCounters CpuUtilizationPercent is null or within 0-100", () =>
                {
                    LinuxSystemCounters sys = new LinuxSystemCounters();
                    double? c1 = sys.CpuUtilizationPercent;
                    Thread.Sleep(150);
                    double? c2 = sys.CpuUtilizationPercent;
                    if (c1.HasValue) Check.InRange(c1.Value, 0, 100, "First CPU sample out of range");
                    if (c2.HasValue) Check.InRange(c2.Value, 0, 100, "Second CPU sample out of range");
                }),

                Case(Id, "MemoryFreePositiveOrNull", "LinuxSystemCounters MemoryFreeMegabytes is null or positive", () =>
                {
                    double? mem = new LinuxSystemCounters().MemoryFreeMegabytes;
                    if (mem.HasValue) Check.IsTrue(mem.Value > 0, "Free memory should be positive");
                }),

                Case(Id, "SystemToStringNotEmpty", "LinuxSystemCounters ToString() is non-empty", () =>
                {
                    Check.IsNotEmpty(new LinuxSystemCounters().ToString(), "ToString should be non-empty");
                }),

                Case(Id, "ProcessCounterNullThrows", "LinuxProcessCounters(null) throws ArgumentNullException (negative)", () =>
                {
                    Check.Throws<ArgumentNullException>(() => new LinuxProcessCounters(null),
                        "Null process should throw ArgumentNullException");
                }),

                Case(Id, "ProcessWindowsFieldsNull", "LinuxProcessCounters returns null for Windows-only memory fields", () =>
                {
                    using (Process p = Process.GetCurrentProcess())
                    using (LinuxProcessCounters proc = new LinuxProcessCounters(p))
                    {
                        Check.IsNull(proc.NonPagedSystemMemory, "NonPagedSystemMemory should be null on Linux");
                        Check.IsNull(proc.PagedSystemMemory, "PagedSystemMemory should be null on Linux");
                        Check.IsNull(proc.PeakPagedSystemMemory, "PeakPagedSystemMemory should be null on Linux");
                    }
                }),
            };

            return new TestSuiteDescriptor(Id, "Linux-Specific Implementation", cases);
        }

        #endregion

        #region Mac

        private static TestSuiteDescriptor BuildMac()
        {
            const string Id = "Mac";

            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Case(Id, "DirectInstantiation", "MacPerformanceStatistics can be instantiated directly", () =>
                {
                    using (MacPerformanceStatistics stats = new MacPerformanceStatistics())
                    {
                        Check.IsNotNull(stats, "Instance should not be null");
                        Check.IsTrue(stats.System is MacSystemCounters, "System should be MacSystemCounters");
                    }
                }),

                Case(Id, "CpuInRangeOrNull", "MacSystemCounters CpuUtilizationPercent is null or within 0-100", () =>
                {
                    double? cpu = new MacSystemCounters().CpuUtilizationPercent;
                    if (cpu.HasValue) Check.InRange(cpu.Value, 0, 100, "CPU utilization out of range");
                }),

                Case(Id, "MemoryFreePositiveOrNull", "MacSystemCounters MemoryFreeMegabytes is null or positive", () =>
                {
                    double? mem = new MacSystemCounters().MemoryFreeMegabytes;
                    if (mem.HasValue) Check.IsTrue(mem.Value > 0, "Free memory should be positive");
                }),

                Case(Id, "DiskQueuesNull", "MacSystemCounters disk queue metrics are null (unavailable)", () =>
                {
                    MacSystemCounters sys = new MacSystemCounters();
                    Check.IsNull(sys.TotalDiskReadQueue, "Read queue is unavailable on macOS");
                    Check.IsNull(sys.TotalDiskWriteQueue, "Write queue is unavailable on macOS");
                }),

                Case(Id, "SystemToStringNotEmpty", "MacSystemCounters ToString() is non-empty", () =>
                {
                    Check.IsNotEmpty(new MacSystemCounters().ToString(), "ToString should be non-empty");
                }),

                Case(Id, "ProcessCounterNullThrows", "MacProcessCounters(null) throws ArgumentNullException (negative)", () =>
                {
                    Check.Throws<ArgumentNullException>(() => new MacProcessCounters(null),
                        "Null process should throw ArgumentNullException");
                }),

                Case(Id, "ProcessHandleCountZero", "MacProcessCounters HandleCount is zero (unavailable)", () =>
                {
                    using (Process p = Process.GetCurrentProcess())
                    using (MacProcessCounters proc = new MacProcessCounters(p))
                    {
                        Check.AreEqual(0, proc.HandleCount, "HandleCount is not available on macOS");
                        Check.IsNull(proc.NonPagedSystemMemory, "NonPagedSystemMemory should be null on macOS");
                    }
                }),
            };

            return new TestSuiteDescriptor(Id, "macOS-Specific Implementation", cases);
        }

        #endregion
    }
}
