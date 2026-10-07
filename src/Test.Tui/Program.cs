using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Touchstone.Cli;
using Touchstone.Core;

namespace Test.Tui
{
    /// <summary>
    /// Runs the headless TUI suites.
    /// </summary>
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            string resultsPath = args != null && args.Length > 0 ? args[0] : null;
            return await ConsoleRunner.RunAsync(new List<TestSuiteDescriptor> { TuiSuite.Build() }, sink: null, resultsPath: resultsPath, cancellationToken: CancellationToken.None).ConfigureAwait(false);
        }
    }
}
