using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using PerformanceStatistics;
using Touchstone.Core;

namespace Test.Shared
{
    /// <summary>
    /// Common helpers shared across the Touchstone test suites.
    /// </summary>
    public static class TestSupport
    {
        /// <summary>
        /// The name of the currently executing process, used as a guaranteed-present
        /// target for process-monitoring tests.
        /// </summary>
        public static string CurrentProcessName
        {
            get
            {
                using (Process p = Process.GetCurrentProcess())
                {
                    return p.ProcessName;
                }
            }
        }

        /// <summary>
        /// A process name that is extremely unlikely to exist, used for negative tests.
        /// </summary>
        public const string NonExistentProcessName = "ThisProcessDoesNotExist_PerfStatsTest_12345";

        /// <summary>
        /// True if the current platform is Windows.
        /// </summary>
        public static bool IsWindows => PerformanceStatisticsFactory.CurrentPlatform == PlatformTypeEnum.Windows;

        /// <summary>
        /// True if the current platform is Linux.
        /// </summary>
        public static bool IsLinux => PerformanceStatisticsFactory.CurrentPlatform == PlatformTypeEnum.Linux;

        /// <summary>
        /// True if the current platform is macOS.
        /// </summary>
        public static bool IsMac => PerformanceStatisticsFactory.CurrentPlatform == PlatformTypeEnum.Mac;

        /// <summary>
        /// Wrap a synchronous test body as a Touchstone test case descriptor.
        /// </summary>
        /// <param name="suiteId">Owning suite identifier.</param>
        /// <param name="caseId">Case identifier, unique within the suite.</param>
        /// <param name="displayName">Human-readable case name.</param>
        /// <param name="body">Synchronous test body; throw to fail.</param>
        /// <returns>Test case descriptor.</returns>
        public static TestCaseDescriptor Case(string suiteId, string caseId, string displayName, Action body)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));

            return new TestCaseDescriptor(
                suiteId,
                caseId,
                displayName,
                executeAsync: ct =>
                {
                    ct.ThrowIfCancellationRequested();
                    body();
                    return Task.CompletedTask;
                });
        }

        /// <summary>
        /// Wrap an asynchronous test body as a Touchstone test case descriptor.
        /// </summary>
        /// <param name="suiteId">Owning suite identifier.</param>
        /// <param name="caseId">Case identifier, unique within the suite.</param>
        /// <param name="displayName">Human-readable case name.</param>
        /// <param name="body">Asynchronous test body; throw to fail.</param>
        /// <returns>Test case descriptor.</returns>
        public static TestCaseDescriptor CaseAsync(string suiteId, string caseId, string displayName, Func<CancellationToken, Task> body)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            return new TestCaseDescriptor(suiteId, caseId, displayName, body);
        }
    }
}
