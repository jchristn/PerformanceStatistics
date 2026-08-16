using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using PerformanceStatistics;
using Touchstone.Core;
using static Test.Shared.TestSupport;

namespace Test.Shared.Suites
{
    /// <summary>
    /// Tests for <see cref="PerformanceStatisticsFactory"/> and <see cref="PlatformTypeEnum"/>.
    /// </summary>
    public static class FactorySuite
    {
        private const string Id = "Factory";

        /// <summary>
        /// Build the factory / platform test suite.
        /// </summary>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Case(Id, "CurrentPlatformNotUnknown", "CurrentPlatform is not Unknown on a supported OS", () =>
                {
                    Check.AreNotEqual(PlatformTypeEnum.Unknown, PerformanceStatisticsFactory.CurrentPlatform,
                        "CurrentPlatform should be a known platform");
                }),

                Case(Id, "CurrentPlatformMatchesRuntime", "CurrentPlatform matches RuntimeInformation", () =>
                {
                    PlatformTypeEnum expected =
                        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? PlatformTypeEnum.Windows :
                        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? PlatformTypeEnum.Linux :
                        RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? PlatformTypeEnum.Mac :
                        PlatformTypeEnum.Unknown;
                    Check.AreEqual(expected, PerformanceStatisticsFactory.CurrentPlatform,
                        "CurrentPlatform should match the actual OS");
                }),

                Case(Id, "IsPlatformSupportedTrue", "IsPlatformSupported is true on a supported OS", () =>
                {
                    Check.IsTrue(PerformanceStatisticsFactory.IsPlatformSupported,
                        "IsPlatformSupported should be true");
                }),

                Case(Id, "CreateReturnsInstance", "Create() returns a non-null instance", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        Check.IsNotNull(stats, "Create() should return a non-null instance");
                    }
                }),

                Case(Id, "CreateReturnsPlatformType", "Create() returns the platform-appropriate implementation", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        switch (PerformanceStatisticsFactory.CurrentPlatform)
                        {
                            case PlatformTypeEnum.Windows:
                                Check.IsTrue(stats is PerformanceStatistics.Windows.WindowsPerformanceStatistics,
                                    "Windows should yield WindowsPerformanceStatistics");
                                break;
                            case PlatformTypeEnum.Linux:
                                Check.IsTrue(stats is PerformanceStatistics.Linux.LinuxPerformanceStatistics,
                                    "Linux should yield LinuxPerformanceStatistics");
                                break;
                            case PlatformTypeEnum.Mac:
                                Check.IsTrue(stats is PerformanceStatistics.Mac.MacPerformanceStatistics,
                                    "Mac should yield MacPerformanceStatistics");
                                break;
                            default:
                                throw new CheckException("Unexpected platform");
                        }
                    }
                }),

                Case(Id, "CreateNullArgument", "Create(null) yields an empty monitored process list", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create(null))
                    {
                        Check.IsNotNull(stats.MonitoredProcessNames, "MonitoredProcessNames should not be null");
                        Check.AreEqual(0, stats.MonitoredProcessNames.Count, "MonitoredProcessNames should be empty");
                    }
                }),

                Case(Id, "CreateWithProcessList", "Create(list) populates monitored process names", () =>
                {
                    List<string> names = new List<string> { "dotnet" };
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create(names))
                    {
                        Check.IsNotNull(stats, "Create() should return a non-null instance");
                        Check.AreEqual(1, stats.MonitoredProcessNames.Count, "Should carry the supplied process name");
                        Check.AreEqual("dotnet", stats.MonitoredProcessNames[0], "Should carry the exact name");
                    }
                }),

                Case(Id, "CreateWithEmptyList", "Create(empty list) yields an empty monitored process list", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create(new List<string>()))
                    {
                        Check.AreEqual(0, stats.MonitoredProcessNames.Count, "MonitoredProcessNames should be empty");
                    }
                }),

                Case(Id, "CreateResultIsDisposable", "Created instance implements IDisposable", () =>
                {
                    IPerformanceStatistics stats = PerformanceStatisticsFactory.Create();
                    Check.IsTrue(stats is IDisposable, "Instance should implement IDisposable");
                    stats.Dispose();
                }),

                Case(Id, "PlatformEnumHasAllValues", "PlatformTypeEnum defines Unknown, Windows, Linux, Mac", () =>
                {
                    Check.IsTrue(Enum.IsDefined(typeof(PlatformTypeEnum), PlatformTypeEnum.Unknown), "Unknown defined");
                    Check.IsTrue(Enum.IsDefined(typeof(PlatformTypeEnum), PlatformTypeEnum.Windows), "Windows defined");
                    Check.IsTrue(Enum.IsDefined(typeof(PlatformTypeEnum), PlatformTypeEnum.Linux), "Linux defined");
                    Check.IsTrue(Enum.IsDefined(typeof(PlatformTypeEnum), PlatformTypeEnum.Mac), "Mac defined");
                }),

                Case(Id, "CreateDistinctInstances", "Create() returns a distinct instance each call", () =>
                {
                    using (IPerformanceStatistics a = PerformanceStatisticsFactory.Create())
                    using (IPerformanceStatistics b = PerformanceStatisticsFactory.Create())
                    {
                        Check.IsFalse(ReferenceEquals(a, b), "Each Create() call should return a new instance");
                    }
                }),

                Case(Id, "IsPlatformSupportedConsistentWithCurrentPlatform", "IsPlatformSupported agrees with CurrentPlatform", () =>
                {
                    bool supported = PerformanceStatisticsFactory.IsPlatformSupported;
                    bool known = PerformanceStatisticsFactory.CurrentPlatform != PlatformTypeEnum.Unknown;
                    Check.AreEqual(known, supported,
                        "IsPlatformSupported should be true exactly when CurrentPlatform is a known platform");
                }),
            };

            return new TestSuiteDescriptor(Id, "Factory & Platform Detection", cases);
        }
    }
}
