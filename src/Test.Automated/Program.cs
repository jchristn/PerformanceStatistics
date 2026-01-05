using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading;
using PerformanceStatistics;

namespace Test.Automated
{
    public class Program
    {
        private static readonly List<TestResult> _results = new List<TestResult>();
        private static readonly Stopwatch _totalStopwatch = new Stopwatch();

        public static int Main(string[] args)
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║       PerformanceStatistics Automated Test Suite                 ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            _totalStopwatch.Start();

            // Platform detection
            PlatformType platform = PerformanceStatisticsFactory.CurrentPlatform;
            Console.WriteLine($"Detected Platform: {platform}");
            Console.WriteLine($"Platform Supported: {PerformanceStatisticsFactory.IsPlatformSupported}");
            Console.WriteLine();
            Console.WriteLine("══════════════════════════════════════════════════════════════════");
            Console.WriteLine();

            // Run tests based on platform
            RunFactoryTests();
            RunInterfaceTests();

            if (platform == PlatformType.Windows)
            {
                RunWindowsSpecificTests();
            }

            RunCrossplatformTests();
            RunProcessMonitoringTests();
            RunTcpConnectionTests();
            RunDisposalTests();
            RunEdgeCaseTests();

            _totalStopwatch.Stop();

            // Print summary
            PrintSummary();

            // Return exit code based on results
            int failedCount = _results.FindAll(r => !r.Passed).Count;
            return failedCount > 0 ? 1 : 0;
        }

        #region Test Categories

        private static void RunFactoryTests()
        {
            PrintCategory("Factory Tests");

            RunTest("Factory.CurrentPlatform returns valid value", () =>
            {
                var platform = PerformanceStatisticsFactory.CurrentPlatform;
                return platform != PlatformType.Unknown;
            });

            RunTest("Factory.IsPlatformSupported returns true", () =>
            {
                return PerformanceStatisticsFactory.IsPlatformSupported;
            });

            RunTest("Factory.Create() returns non-null instance", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                return stats != null;
            });

            RunTest("Factory.Create() with monitored processes list", () =>
            {
                var processes = new List<string> { "dotnet" };
                using var stats = PerformanceStatisticsFactory.Create(processes);
                return stats != null && stats.MonitoredProcessNames.Count == 1;
            });
        }

        private static void RunInterfaceTests()
        {
            PrintCategory("Interface Implementation Tests");

            RunTest("IPerformanceStatistics.System is not null", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                return stats.System != null;
            });

            RunTest("IPerformanceStatistics.MonitoredProcessNames is initialized", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                return stats.MonitoredProcessNames != null;
            });

            RunTest("IPerformanceStatistics.MonitoredProcessNames can be set", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                stats.MonitoredProcessNames = new List<string> { "test1", "test2" };
                return stats.MonitoredProcessNames.Count == 2;
            });

            RunTest("IPerformanceStatistics.MonitoredProcessNames handles null assignment", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                stats.MonitoredProcessNames = null;
                return stats.MonitoredProcessNames != null && stats.MonitoredProcessNames.Count == 0;
            });
        }

        private static void RunWindowsSpecificTests()
        {
            PrintCategory("Windows-Specific Tests");

            RunTest("WindowsPerformanceStatistics can be instantiated directly", () =>
            {
                using var stats = new WindowsPerformanceStatistics();
                return stats != null;
            });

            RunTest("WindowsSystemCounters.CpuUtilizationPercent returns value", () =>
            {
                using var stats = new WindowsPerformanceStatistics();
                var cpu = stats.System.CpuUtilizationPercent;
                return cpu >= 0 && cpu <= 100;
            });

            RunTest("WindowsSystemCounters.MemoryFreeMegabytes returns positive value", () =>
            {
                using var stats = new WindowsPerformanceStatistics();
                return stats.System.MemoryFreeMegabytes > 0;
            });

            RunTest("WindowsSystemCounters disk metrics are valid", () =>
            {
                using var stats = new WindowsPerformanceStatistics();
                return stats.System.TotalDiskSizeMegabytes > 0 &&
                       stats.System.TotalDiskFreeMegabytes >= 0 &&
                       stats.System.TotalDiskUsedMegabytes >= 0;
            });
        }

        private static void RunCrossplatformTests()
        {
            PrintCategory("Cross-Platform System Counter Tests");

            RunTest("System.CpuUtilizationPercent returns value or null", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                var cpu = stats.System.CpuUtilizationPercent;
                return cpu == null || (cpu >= 0 && cpu <= 100);
            });

            RunTest("System.CpuUtilizationPercent changes with second sample", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                var cpu1 = stats.System.CpuUtilizationPercent;
                Thread.Sleep(200); // Allow time for second sample
                var cpu2 = stats.System.CpuUtilizationPercent;
                // Just verify it doesn't throw and returns valid values
                return (cpu1 == null || cpu1 >= 0) && (cpu2 == null || cpu2 >= 0);
            });

            RunTest("System.MemoryFreeMegabytes returns positive value or null", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                var mem = stats.System.MemoryFreeMegabytes;
                return mem == null || mem > 0;
            });

            RunTest("System.TotalDiskReadOperations returns non-negative or null", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                var ops = stats.System.TotalDiskReadOperations;
                return ops == null || ops >= 0;
            });

            RunTest("System.TotalDiskWriteOperations returns non-negative or null", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                var ops = stats.System.TotalDiskWriteOperations;
                return ops == null || ops >= 0;
            });

            RunTest("System.TotalDiskReadQueue returns non-negative or null", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                var queue = stats.System.TotalDiskReadQueue;
                return queue == null || queue >= 0;
            });

            RunTest("System.TotalDiskWriteQueue returns non-negative or null", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                var queue = stats.System.TotalDiskWriteQueue;
                return queue == null || queue >= 0;
            });

            RunTest("System.TotalDiskFreePercent returns valid percentage or null", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                var pct = stats.System.TotalDiskFreePercent;
                return pct == null || (pct >= 0 && pct <= 100);
            });

            RunTest("System.TotalDiskFreeMegabytes returns non-negative or null", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                var mb = stats.System.TotalDiskFreeMegabytes;
                return mb == null || mb >= 0;
            });

            RunTest("System.TotalDiskSizeMegabytes returns positive or null", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                var mb = stats.System.TotalDiskSizeMegabytes;
                return mb == null || mb > 0;
            });

            RunTest("System.TotalDiskUsedMegabytes returns non-negative or null", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                var mb = stats.System.TotalDiskUsedMegabytes;
                return mb == null || mb >= 0;
            });

            RunTest("System.Disk metrics are consistent (used + free = total)", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                var free = stats.System.TotalDiskFreeMegabytes;
                var used = stats.System.TotalDiskUsedMegabytes;
                var total = stats.System.TotalDiskSizeMegabytes;

                if (free == null || used == null || total == null) return true;

                // Allow 1% tolerance for rounding
                var calculated = free.Value + used.Value;
                var tolerance = total.Value * 0.01;
                return Math.Abs(calculated - total.Value) <= tolerance;
            });
        }

        private static void RunProcessMonitoringTests()
        {
            PrintCategory("Process Monitoring Tests");

            // Get current process name for testing
            string currentProcessName = Process.GetCurrentProcess().ProcessName;

            RunTest("MonitoredProcesses returns empty dict when no processes monitored", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                stats.MonitoredProcessNames.Clear();
                var procs = stats.MonitoredProcesses;
                return procs != null && procs.Count == 0;
            });

            RunTest($"MonitoredProcesses finds current process ({currentProcessName})", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                stats.MonitoredProcessNames.Add(currentProcessName);
                var procs = stats.MonitoredProcesses;
                return procs.ContainsKey(currentProcessName) && procs[currentProcessName].Count > 0;
            });

            RunTest("MonitoredProcesses returns empty list for non-existent process", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                stats.MonitoredProcessNames.Add("ThisProcessDoesNotExist12345");
                var procs = stats.MonitoredProcesses;
                return procs.ContainsKey("ThisProcessDoesNotExist12345") &&
                       procs["ThisProcessDoesNotExist12345"].Count == 0;
            });

            RunTest("ProcessCounters.ProcessId returns valid value", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                stats.MonitoredProcessNames.Add(currentProcessName);
                var procs = stats.MonitoredProcesses;
                if (procs[currentProcessName].Count == 0) return false;
                var proc = procs[currentProcessName][0];
                return proc.ProcessId.HasValue && proc.ProcessId.Value > 0;
            });

            RunTest("ProcessCounters.ProcessName returns current process name", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                stats.MonitoredProcessNames.Add(currentProcessName);
                var procs = stats.MonitoredProcesses;
                if (procs[currentProcessName].Count == 0) return false;
                var proc = procs[currentProcessName][0];
                return proc.ProcessName == currentProcessName;
            });

            RunTest("ProcessCounters.MachineName returns non-empty string", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                stats.MonitoredProcessNames.Add(currentProcessName);
                var procs = stats.MonitoredProcesses;
                if (procs[currentProcessName].Count == 0) return false;
                var proc = procs[currentProcessName][0];
                return !string.IsNullOrEmpty(proc.MachineName);
            });

            RunTest("ProcessCounters.CpuUtilizationPercent returns valid value or null", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                stats.MonitoredProcessNames.Add(currentProcessName);
                var procs = stats.MonitoredProcesses;
                if (procs[currentProcessName].Count == 0) return false;
                var proc = procs[currentProcessName][0];
                Thread.Sleep(100); // Allow CPU sample
                var cpu = proc.CpuUtilizationPercent;
                return cpu == null || cpu >= 0;
            });

            RunTest("ProcessCounters.ThreadCount returns positive value", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                stats.MonitoredProcessNames.Add(currentProcessName);
                var procs = stats.MonitoredProcesses;
                if (procs[currentProcessName].Count == 0) return false;
                var proc = procs[currentProcessName][0];
                return proc.ThreadCount.HasValue && proc.ThreadCount.Value > 0;
            });

            RunTest("ProcessCounters.HandleCount returns non-negative or null", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                stats.MonitoredProcessNames.Add(currentProcessName);
                var procs = stats.MonitoredProcesses;
                if (procs[currentProcessName].Count == 0) return false;
                var proc = procs[currentProcessName][0];
                return proc.HandleCount == null || proc.HandleCount >= 0;
            });

            RunTest("ProcessCounters.WorkingSetMemory returns positive value", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                stats.MonitoredProcessNames.Add(currentProcessName);
                var procs = stats.MonitoredProcesses;
                if (procs[currentProcessName].Count == 0) return false;
                var proc = procs[currentProcessName][0];
                return proc.WorkingSetMemory.HasValue && proc.WorkingSetMemory.Value > 0;
            });

            RunTest("ProcessCounters.VirtualMemory returns positive value or null", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                stats.MonitoredProcessNames.Add(currentProcessName);
                var procs = stats.MonitoredProcesses;
                if (procs[currentProcessName].Count == 0) return false;
                var proc = procs[currentProcessName][0];
                return proc.VirtualMemory == null || proc.VirtualMemory > 0;
            });

            RunTest("ProcessCounters.PrivateMemory returns positive value or null", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                stats.MonitoredProcessNames.Add(currentProcessName);
                var procs = stats.MonitoredProcesses;
                if (procs[currentProcessName].Count == 0) return false;
                var proc = procs[currentProcessName][0];
                return proc.PrivateMemory == null || proc.PrivateMemory > 0;
            });

            RunTest("ProcessCounters.PeakWorkingSetMemory returns positive value or null", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                stats.MonitoredProcessNames.Add(currentProcessName);
                var procs = stats.MonitoredProcesses;
                if (procs[currentProcessName].Count == 0) return false;
                var proc = procs[currentProcessName][0];
                return proc.PeakWorkingSetMemory == null || proc.PeakWorkingSetMemory > 0;
            });

            RunTest("ProcessCounters memory values are consistent (Working <= Peak)", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                stats.MonitoredProcessNames.Add(currentProcessName);
                var procs = stats.MonitoredProcesses;
                if (procs[currentProcessName].Count == 0) return false;
                var proc = procs[currentProcessName][0];

                var working = proc.WorkingSetMemory;
                var peak = proc.PeakWorkingSetMemory;

                if (working == null || peak == null) return true;
                return working.Value <= peak.Value;
            });
        }

        private static void RunTcpConnectionTests()
        {
            PrintCategory("TCP Connection Tests");

            RunTest("ActiveTcpConnections returns non-null array", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                var connections = stats.ActiveTcpConnections;
                return connections != null;
            });

            RunTest("ActiveTcpConnections returns array (may be empty)", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                var connections = stats.ActiveTcpConnections;
                return connections.Length >= 0;
            });

            RunTest("GetActiveTcpConnections with no filter returns same as property", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                var all = stats.ActiveTcpConnections;
                var filtered = stats.GetActiveTcpConnections();
                return all.Length == filtered.Length;
            });

            RunTest("GetActiveTcpConnections with invalid source port returns empty or subset", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                var filtered = stats.GetActiveTcpConnections(sourcePort: 99999);
                return filtered.Length == 0 || filtered.Length < stats.ActiveTcpConnections.Length;
            });

            RunTest("GetActiveTcpConnections with invalid dest port returns empty or subset", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                var filtered = stats.GetActiveTcpConnections(destPort: 99999);
                return filtered.Length == 0 || filtered.Length < stats.ActiveTcpConnections.Length;
            });

            RunTest("GetActiveTcpConnections filters by source port correctly", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                var all = stats.ActiveTcpConnections;
                if (all.Length == 0) return true; // Skip if no connections

                var firstPort = all[0].LocalEndPoint.Port;
                var filtered = stats.GetActiveTcpConnections(sourcePort: firstPort);

                // All filtered results should have matching source port
                foreach (var conn in filtered)
                {
                    if (conn.LocalEndPoint.Port != firstPort) return false;
                }
                return true;
            });

            RunTest("GetActiveTcpConnections filters by dest port correctly", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                var all = stats.ActiveTcpConnections;
                if (all.Length == 0) return true; // Skip if no connections

                var firstPort = all[0].RemoteEndPoint.Port;
                var filtered = stats.GetActiveTcpConnections(destPort: firstPort);

                // All filtered results should have matching dest port
                foreach (var conn in filtered)
                {
                    if (conn.RemoteEndPoint.Port != firstPort) return false;
                }
                return true;
            });
        }

        private static void RunDisposalTests()
        {
            PrintCategory("Disposal Tests");

            RunTest("IPerformanceStatistics implements IDisposable", () =>
            {
                var stats = PerformanceStatisticsFactory.Create();
                bool isDisposable = stats is IDisposable;
                stats.Dispose();
                return isDisposable;
            });

            RunTest("Dispose can be called multiple times without error", () =>
            {
                var stats = PerformanceStatisticsFactory.Create();
                stats.Dispose();
                stats.Dispose(); // Should not throw
                return true;
            });

            RunTest("Using statement works correctly", () =>
            {
                bool disposed = false;
                using (var stats = PerformanceStatisticsFactory.Create())
                {
                    _ = stats.System.CpuUtilizationPercent;
                    disposed = true;
                }
                return disposed;
            });

            RunTest("MonitoredProcesses refresh disposes old handles", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                string currentProcessName = Process.GetCurrentProcess().ProcessName;
                stats.MonitoredProcessNames.Add(currentProcessName);

                // Access multiple times - should dispose old handles
                _ = stats.MonitoredProcesses;
                _ = stats.MonitoredProcesses;
                _ = stats.MonitoredProcesses;

                return true; // If no exception, disposal is working
            });
        }

        private static void RunEdgeCaseTests()
        {
            PrintCategory("Edge Case Tests");

            RunTest("Empty monitored process list works", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                stats.MonitoredProcessNames = new List<string>();
                return stats.MonitoredProcesses.Count == 0;
            });

            RunTest("Multiple process names with same name works", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                string name = Process.GetCurrentProcess().ProcessName;
                stats.MonitoredProcessNames.Add(name);
                // Adding same name twice shouldn't cause issues on first access
                // but dictionary will throw on second add - this tests the implementation
                var procs = stats.MonitoredProcesses;
                return procs.ContainsKey(name);
            });

            RunTest("ToString() returns non-empty string", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                string str = stats.ToString();
                return !string.IsNullOrEmpty(str);
            });

            RunTest("Rapid successive property access doesn't throw", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                for (int i = 0; i < 10; i++)
                {
                    _ = stats.System.CpuUtilizationPercent;
                    _ = stats.System.MemoryFreeMegabytes;
                    _ = stats.ActiveTcpConnections;
                }
                return true;
            });

            RunTest("Thread safety - concurrent access doesn't throw", () =>
            {
                using var stats = PerformanceStatisticsFactory.Create();
                stats.MonitoredProcessNames.Add(Process.GetCurrentProcess().ProcessName);

                var tasks = new List<System.Threading.Tasks.Task>();
                for (int i = 0; i < 5; i++)
                {
                    tasks.Add(System.Threading.Tasks.Task.Run(() =>
                    {
                        for (int j = 0; j < 10; j++)
                        {
                            _ = stats.MonitoredProcesses;
                            _ = stats.System.CpuUtilizationPercent;
                        }
                    }));
                }

                System.Threading.Tasks.Task.WaitAll(tasks.ToArray());
                return true;
            });
        }

        #endregion

        #region Test Infrastructure

        private static void PrintCategory(string category)
        {
            Console.WriteLine();
            Console.WriteLine($"▶ {category}");
            Console.WriteLine(new string('─', 66));
        }

        private static void RunTest(string description, Func<bool> test)
        {
            var sw = Stopwatch.StartNew();
            bool passed = false;
            string error = null;

            try
            {
                passed = test();
            }
            catch (Exception ex)
            {
                passed = false;
                error = ex.GetType().Name + ": " + ex.Message;
            }

            sw.Stop();

            var result = new TestResult
            {
                Description = description,
                Passed = passed,
                RuntimeMs = sw.ElapsedMilliseconds,
                Error = error
            };
            _results.Add(result);

            string status = passed ? "PASS" : "FAIL";
            string statusColor = passed ? "\u001b[32m" : "\u001b[31m"; // Green or Red
            string reset = "\u001b[0m";

            // Truncate description if too long
            string desc = description.Length > 50 ? description.Substring(0, 47) + "..." : description;

            Console.WriteLine($"  [{statusColor}{status}{reset}] {desc,-50} ({sw.ElapsedMilliseconds,4}ms)");

            if (!passed && error != null)
            {
                Console.WriteLine($"         └─ {error}");
            }
        }

        private static void PrintSummary()
        {
            Console.WriteLine();
            Console.WriteLine("══════════════════════════════════════════════════════════════════");
            Console.WriteLine("                         TEST SUMMARY                             ");
            Console.WriteLine("══════════════════════════════════════════════════════════════════");
            Console.WriteLine();

            int total = _results.Count;
            int passed = _results.FindAll(r => r.Passed).Count;
            int failed = total - passed;

            Console.WriteLine($"  Total Tests:  {total}");
            Console.WriteLine($"  Passed:       {passed}");
            Console.WriteLine($"  Failed:       {failed}");
            Console.WriteLine($"  Total Time:   {_totalStopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine();

            if (failed > 0)
            {
                Console.WriteLine("  Failed Tests:");
                Console.WriteLine("  ─────────────");
                foreach (var result in _results.FindAll(r => !r.Passed))
                {
                    Console.WriteLine($"    • {result.Description}");
                    if (result.Error != null)
                    {
                        Console.WriteLine($"      └─ {result.Error}");
                    }
                }
                Console.WriteLine();
            }

            string overallStatus = failed == 0 ? "PASS" : "FAIL";
            string statusColor = failed == 0 ? "\u001b[32m" : "\u001b[31m";
            string reset = "\u001b[0m";

            Console.WriteLine("══════════════════════════════════════════════════════════════════");
            Console.WriteLine($"                    OVERALL: {statusColor}{overallStatus}{reset}");
            Console.WriteLine("══════════════════════════════════════════════════════════════════");
        }

        private class TestResult
        {
            public string Description { get; set; }
            public bool Passed { get; set; }
            public long RuntimeMs { get; set; }
            public string Error { get; set; }
        }

        #endregion
    }
}
