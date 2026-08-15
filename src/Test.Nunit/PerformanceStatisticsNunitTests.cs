using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Test.Shared;
using Touchstone.Core;
using Touchstone.NunitAdapter;

namespace Test.Nunit
{
    /// <summary>
    /// NUnit host for the shared Touchstone suites. Each non-skipped shared test case
    /// becomes one NUnit test via the Touchstone NUnit adapter
    /// (<see cref="TouchstoneTestCaseSource"/>), so the exact same assertions defined in
    /// Test.Shared run under NUnit.
    /// </summary>
    [TestFixture]
    public sealed class PerformanceStatisticsNunitTests
    {
        /// <summary>
        /// One NUnit test case per non-skipped shared test case descriptor.
        /// </summary>
        public static IEnumerable TestCases()
        {
            return new TouchstoneTestCaseSource(PerformanceStatisticsSuites.All);
        }

        /// <summary>
        /// Execute a single shared test case; it fails by throwing.
        /// </summary>
        /// <param name="testCase">The shared test case descriptor.</param>
        [TestCaseSource(nameof(TestCases))]
        public async Task Run(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None);
        }
    }
}
