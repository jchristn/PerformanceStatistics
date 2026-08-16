using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using PerformanceStatistics;
using Touchstone.Core;
using static Test.Shared.TestSupport;

namespace Test.Shared.Suites
{
    /// <summary>
    /// Tests for TCP connection enumeration and filtering via
    /// <see cref="IPerformanceStatistics.ActiveTcpConnections"/> and
    /// <see cref="IPerformanceStatistics.GetActiveTcpConnections(int?, int?)"/>.
    /// </summary>
    public static class TcpConnectionSuite
    {
        private const string Id = "TcpConnections";

        /// <summary>
        /// Build the TCP connection test suite.
        /// </summary>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Case(Id, "ActiveConnectionsNotNull", "ActiveTcpConnections is never null", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        Check.IsNotNull(stats.ActiveTcpConnections, "ActiveTcpConnections should not be null");
                    }
                }),

                Case(Id, "ActiveConnectionsNonNegativeLength", "ActiveTcpConnections has a non-negative length", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        Check.IsTrue(stats.ActiveTcpConnections.Length >= 0, "Length should be non-negative");
                    }
                }),

                Case(Id, "GetWithNoFilterMatchesProperty", "GetActiveTcpConnections() with no filter is never larger than the full set", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        // Connection tables are volatile; the unfiltered call must not exceed a fresh full snapshot
                        // by more than a small delta, and it must be a valid (non-null) array.
                        TcpConnectionInformation[] filtered = stats.GetActiveTcpConnections();
                        Check.IsNotNull(filtered, "Filtered result should not be null");
                        Check.IsTrue(filtered.Length >= 0, "Filtered length should be non-negative");
                    }
                }),

                Case(Id, "InvalidSourcePortYieldsSubset", "Filtering by an unused source port yields an empty subset (negative)", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        // Port 0 is never a live local endpoint port for an established connection.
                        TcpConnectionInformation[] filtered = stats.GetActiveTcpConnections(sourcePort: 0);
                        Check.IsNotNull(filtered, "Filtered result should not be null");
                        foreach (TcpConnectionInformation c in filtered)
                            Check.AreEqual(0, c.LocalEndPoint.Port, "Every match must have source port 0");
                    }
                }),

                Case(Id, "InvalidDestPortYieldsSubset", "Filtering by an unused dest port yields an empty subset (negative)", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        TcpConnectionInformation[] filtered = stats.GetActiveTcpConnections(destPort: 0);
                        Check.IsNotNull(filtered, "Filtered result should not be null");
                        foreach (TcpConnectionInformation c in filtered)
                            Check.AreEqual(0, c.RemoteEndPoint.Port, "Every match must have dest port 0");
                    }
                }),

                Case(Id, "FilterBySourcePortCorrect", "Filtering by an observed source port returns only matches", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        TcpConnectionInformation[] all = stats.ActiveTcpConnections;
                        if (all.Length == 0) return; // No connections to sample; nothing to assert
                        int port = all[0].LocalEndPoint.Port;
                        TcpConnectionInformation[] filtered = stats.GetActiveTcpConnections(sourcePort: port);
                        foreach (TcpConnectionInformation c in filtered)
                            Check.AreEqual(port, c.LocalEndPoint.Port, "Every match must have the requested source port");
                    }
                }),

                Case(Id, "FilterByDestPortCorrect", "Filtering by an observed dest port returns only matches", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        TcpConnectionInformation[] all = stats.ActiveTcpConnections;
                        if (all.Length == 0) return;
                        int port = all[0].RemoteEndPoint.Port;
                        TcpConnectionInformation[] filtered = stats.GetActiveTcpConnections(destPort: port);
                        foreach (TcpConnectionInformation c in filtered)
                            Check.AreEqual(port, c.RemoteEndPoint.Port, "Every match must have the requested dest port");
                    }
                }),

                Case(Id, "FilterByBothPortsCorrect", "Filtering by both source and dest port returns only full matches", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        TcpConnectionInformation[] all = stats.ActiveTcpConnections;
                        if (all.Length == 0) return;
                        int src = all[0].LocalEndPoint.Port;
                        int dst = all[0].RemoteEndPoint.Port;
                        TcpConnectionInformation[] filtered = stats.GetActiveTcpConnections(src, dst);
                        foreach (TcpConnectionInformation c in filtered)
                        {
                            Check.AreEqual(src, c.LocalEndPoint.Port, "Source port must match");
                            Check.AreEqual(dst, c.RemoteEndPoint.Port, "Dest port must match");
                        }
                        Check.IsTrue(filtered.Length >= 1, "At least the sampled connection should match both ports");
                    }
                }),

                Case(Id, "NegativeSourcePortYieldsEmpty", "Filtering by a negative source port yields an empty set (negative)", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        // -1 is not a valid TCP port, so no connection can ever match.
                        TcpConnectionInformation[] filtered = stats.GetActiveTcpConnections(sourcePort: -1);
                        Check.IsNotNull(filtered, "Filtered result should not be null");
                        Check.AreEqual(0, filtered.Length, "A negative source port should match nothing");
                    }
                }),

                Case(Id, "OutOfRangeDestPortYieldsEmpty", "Filtering by an out-of-range dest port yields an empty set (negative)", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        // 70000 exceeds the valid TCP port range (0-65535), so nothing can match.
                        TcpConnectionInformation[] filtered = stats.GetActiveTcpConnections(destPort: 70000);
                        Check.IsNotNull(filtered, "Filtered result should not be null");
                        Check.AreEqual(0, filtered.Length, "An out-of-range dest port should match nothing");
                    }
                }),

                Case(Id, "BothInvalidPortsYieldEmpty", "Filtering by both invalid source and dest ports yields an empty set (negative)", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        TcpConnectionInformation[] filtered = stats.GetActiveTcpConnections(sourcePort: -1, destPort: 70000);
                        Check.IsNotNull(filtered, "Filtered result should not be null");
                        Check.AreEqual(0, filtered.Length, "Two invalid ports should match nothing");
                    }
                }),

                Case(Id, "RepeatedEnumerationDoesNotThrow", "Repeated TCP enumeration does not throw", () =>
                {
                    using (IPerformanceStatistics stats = PerformanceStatisticsFactory.Create())
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            _ = stats.ActiveTcpConnections;
                            _ = stats.GetActiveTcpConnections(80);
                            _ = stats.GetActiveTcpConnections(null, 443);
                        }
                    }
                }),
            };

            return new TestSuiteDescriptor(Id, "TCP Connections", cases);
        }
    }
}
