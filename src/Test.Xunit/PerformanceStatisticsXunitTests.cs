using System.Threading;
using System.Threading.Tasks;
using Test.Shared;
using Touchstone.Core;
using Touchstone.XunitAdapter;
using Xunit;

namespace Test.Xunit
{
    /// <summary>
    /// xUnit host for the shared Touchstone suites. Each shared test case becomes one
    /// xUnit theory row via the Touchstone xUnit adapter (<see cref="TouchstoneTheoryData"/>),
    /// so the exact same assertions defined in Test.Shared run under xUnit.
    /// </summary>
    public sealed class PerformanceStatisticsXunitTests
    {
        /// <summary>
        /// One theory row per shared test case descriptor.
        /// </summary>
        public static TheoryData<TestCaseDescriptor> TestCases()
        {
            return new TouchstoneTheoryData(PerformanceStatisticsSuites.All);
        }

        /// <summary>
        /// Execute a single shared test case; it fails by throwing.
        /// </summary>
        /// <param name="testCase">The shared test case descriptor.</param>
        [Theory]
        [MemberData(nameof(TestCases))]
        public async Task Run(TestCaseDescriptor testCase)
        {
            if (testCase.Skip)
            {
                Assert.True(true, testCase.SkipReason);
                return;
            }

            await testCase.ExecuteAsync(CancellationToken.None);
        }
    }
}
