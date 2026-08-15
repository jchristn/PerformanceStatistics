using System.Collections.Generic;
using Test.Shared.Suites;
using Touchstone.Core;

namespace Test.Shared
{
    /// <summary>
    /// Central source of truth for the PerformanceStatistics test suites.
    /// Every runner (Touchstone console, xUnit, NUnit) consumes <see cref="All"/>,
    /// so a test defined once here executes identically under all three hosts.
    /// The platform-specific suite is included only for the current OS.
    /// </summary>
    public static class PerformanceStatisticsSuites
    {
        /// <summary>
        /// All test suites applicable to the current platform.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                List<TestSuiteDescriptor> suites = new List<TestSuiteDescriptor>
                {
                    FactorySuite.Build(),
                    SystemCountersSuite.Build(),
                    ProcessMonitoringSuite.Build(),
                    TcpConnectionSuite.Build(),
                    DisposalSuite.Build(),
                    EdgeCaseSuite.Build(),
                };

                TestSuiteDescriptor platform = PlatformSpecificSuite.BuildForCurrentPlatform();
                if (platform != null) suites.Add(platform);

                return suites;
            }
        }
    }
}
