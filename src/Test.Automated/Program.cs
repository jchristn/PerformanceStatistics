using System;
using System.Threading;
using System.Threading.Tasks;
using PerformanceStatistics;
using Test.Shared;
using Touchstone.Cli;

namespace Test.Automated
{
    /// <summary>
    /// Automated test runner for PerformanceStatistics using the Touchstone console runner.
    /// The test cases themselves live in Test.Shared (the single source of truth) and are
    /// exposed identically to the xUnit and NUnit adapters.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Entry point. Executes every suite via the Touchstone console runner and returns
        /// a non-zero exit code if any test fails.
        /// </summary>
        /// <param name="args">Optional single argument: a path to which JSON results are written.</param>
        /// <returns>Process exit code (0 = all passed, non-zero = failures).</returns>
        public static async Task<int> Main(string[] args)
        {
            Console.WriteLine("PerformanceStatistics Automated Test Suite (Touchstone)");
            Console.WriteLine("Detected platform : " + PerformanceStatisticsFactory.CurrentPlatform);
            Console.WriteLine("Platform supported: " + PerformanceStatisticsFactory.IsPlatformSupported);
            Console.WriteLine();

            string resultsPath = (args != null && args.Length > 0) ? args[0] : null;

            return await ConsoleRunner.RunAsync(
                PerformanceStatisticsSuites.All,
                sink: null,
                resultsPath: resultsPath,
                cancellationToken: CancellationToken.None);
        }
    }
}
