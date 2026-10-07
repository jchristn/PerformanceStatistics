using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using PerformanceStatistics;
using PerformanceStatistics.Monitoring;
using Touchstone.Core;
using static Test.Shared.TestSupport;

namespace Test.Shared.Suites
{
    /// <summary>
    /// Tests for the PerformanceStatistics.Monitoring layer: sampler, snapshots, history, catalog, formatting,
    /// accounting, action policy, JSON, and SQLite storage with downsampling.
    /// </summary>
    public static class MonitoringSuite
    {
        private const string Id = "Monitoring";

        /// <summary>
        /// Build the monitoring test suite.
        /// </summary>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Case(Id, "SamplerProducesSnapshots", "MetricsSampler produces snapshots with system, process, network, and volume data", () =>
                {
                    using (MetricsSampler sampler = new MetricsSampler("test-host"))
                    {
                        HostSnapshot first = sampler.SampleNow();
                        Thread.Sleep(300);
                        HostSnapshot second = sampler.SampleNow();
                        Check.AreEqual("test-host", second.HostId, "HostId");
                        Check.IsTrue(second.IntervalSeconds > 0.2, "IntervalSeconds should reflect the gap");
                        Check.IsTrue(second.System.CpuPercent.HasValue, "System CPU");
                        Check.AreEqual(Environment.ProcessorCount, second.System.PerCoreCpuPercent.Length, "Per-core CPU");
                        Check.IsTrue(second.System.MemoryTotalBytes > 0 && second.System.MemoryUsedBytes > 0, "Memory");
                        Check.IsTrue(second.Processes.Count > 1, "Processes");
                        Check.AreEqual(second.Processes.Count, second.System.ProcessCount, "ProcessCount matches the list");
                        Check.IsTrue(second.Volumes.Count > 0, "Volumes");
                        Check.IsTrue(ReferenceEquals(second, sampler.Latest), "Latest");
                        Check.IsNotEmpty(sampler.HostInfo.OperatingSystem, "HostInfo");
                    }
                }),

                CaseAsync(Id, "SamplerLoopRaisesEvents", "The background loop raises SnapshotTaken on its interval and stops cleanly", async ct =>
                {
                    using (MetricsSampler sampler = new MetricsSampler())
                    {
                        int count = 0;
                        sampler.Interval = TimeSpan.FromMilliseconds(250);
                        sampler.IncludeProcesses = false;
                        sampler.SnapshotTaken += (s, e) => Interlocked.Increment(ref count);
                        sampler.Start();
                        await System.Threading.Tasks.Task.Delay(1200, ct).ConfigureAwait(false);
                        sampler.Stop();
                        int afterStop = count;
                        Check.IsTrue(afterStop >= 3, "Expected at least 3 snapshots, got " + afterStop);
                        await System.Threading.Tasks.Task.Delay(400, ct).ConfigureAwait(false);
                        Check.AreEqual(afterStop, count, "No snapshots after Stop");
                    }
                }),

                Case(Id, "HistoryKeepsWindowAndExitedProcesses", "HistoryBuffer serves series, caps capacity, and keeps exited processes until they age out", () =>
                {
                    HistoryBuffer history = new HistoryBuffer(5, 5);
                    DateTime t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    for (int i = 0; i < 8; i++)
                    {
                        List<ProcessSnapshot> procs = new List<ProcessSnapshot> { Proc(100, "keeper", t0, cpu: i) };
                        if (i < 2) procs.Add(Proc(200, "short", t0, cpu: 50));
                        history.Add(Snapshot(t0.AddSeconds(i), cpu: i * 10, procs: procs));
                    }

                    List<SeriesPoint> cpu = history.GetSystemSeries(MetricIds.SystemCpu);
                    Check.AreEqual(5, cpu.Count, "Capped at capacity");
                    Check.AreEqual(70.0, cpu.Last().Value, "Newest last");
                    Check.AreEqual(5, history.GetProcessSeries(Proc(100, "keeper", t0).Key, MetricIds.ProcessCpu).Count, "Process series");
                    Check.IsFalse(history.GetKnownProcesses().Any(p => p.Key.Name == "short"), "Exited process aged out of the window");
                    Check.AreEqual(0, history.GetProcessSeries("nope", MetricIds.ProcessCpu).Count, "Unknown process");
                }),

                Case(Id, "CatalogIsConsistent", "Every catalog metric has a unique ID, a unit, a description, and a selector", () =>
                {
                    HashSet<string> ids = new HashSet<string>();
                    foreach (MetricDefinition m in MetricCatalog.All)
                    {
                        Check.IsTrue(ids.Add(m.Id), "Duplicate metric " + m.Id);
                        Check.IsNotEmpty(m.Name, m.Id + " name");
                        Check.IsNotEmpty(m.Description, m.Id + " description");
                        foreach (KeyValuePair<string, MetricAvailability> a in m.Availability)
                        {
                            Check.IsTrue(a.Key == "Windows" || a.Key == "Linux" || a.Key == "Mac", m.Id + " platform " + a.Key);
                            if (a.Value.Level != AvailabilityEnum.Full) Check.IsNotEmpty(a.Value.Note, m.Id + " needs a reason for " + a.Key);
                        }
                    }

                    Check.IsNotNull(MetricCatalog.Get("system.cpu.core.3"), "Per-core IDs resolve");
                    Check.AreEqual(42.0, MetricCatalog.SystemValue("system.cpu.core.1", new SystemSnapshot { PerCoreCpuPercent = new double[] { 1, 42 } }), "Per-core selector");
                    Check.AreEqual(AvailabilityEnum.Unavailable, MetricCatalog.Get(MetricIds.SystemLoad1).For("Windows").Level, "Load average unavailable on Windows");
                }),

                Case(Id, "FormatterIsConsistent", "MetricFormatter formats units identically and shows n/a for null", () =>
                {
                    Check.AreEqual("n/a", MetricFormatter.Format(null, MetricUnitEnum.Bytes), "null");
                    Check.AreEqual("1.5 GiB", MetricFormatter.Format(1.5 * 1024 * 1024 * 1024, MetricUnitEnum.Bytes), "GiB");
                    Check.AreEqual("512 B/s", MetricFormatter.Format(512, MetricUnitEnum.BytesPerSecond), "B/s");
                    Check.AreEqual("12.3%", MetricFormatter.Format(12.34, MetricUnitEnum.Percent), "Percent");
                    Check.AreEqual("1d 01:01:01", MetricFormatter.Format(90061, MetricUnitEnum.Seconds), "Duration");
                    Check.AreEqual("3", MetricFormatter.Format(3.0, MetricUnitEnum.Count), "Count");
                }),

                Case(Id, "AccountantIntegrates", "ProcessAccountant integrates CPU-seconds, disk bytes, and peak memory by name and user", () =>
                {
                    ProcessAccountant accountant = new ProcessAccountant(4);
                    DateTime t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    for (int i = 0; i < 10; i++)
                    {
                        HostSnapshot s = Snapshot(t0.AddSeconds(i), procs: new List<ProcessSnapshot>
                        {
                            Proc(1, "web", t0, cpu: 25, ws: 100 + i, read: 1000, user: "alice"),
                            Proc(2, "web", t0, cpu: 25, ws: 200, user: "bob"),
                            Proc(3, "db", t0, cpu: 0, ws: 50, user: "alice")
                        });
                        s.IntervalSeconds = i == 0 ? 0 : 1;
                        accountant.Add(s);
                    }

                    List<AccountingRow> byName = accountant.Report(AccountingGroupByEnum.Name);
                    AccountingRow web = byName.First(r => r.Group == "web");
                    Check.AreEqual(9 * 2 * 1.0, web.CpuSeconds, "25% of 4 cores = 1 core-second per second per process, 9 intervals, 2 processes");
                    Check.AreEqual(50.0, web.AverageCpuPercent, "Average CPU");
                    Check.AreEqual(9000.0, web.DiskReadBytes, "Disk bytes");
                    Check.AreEqual(2, web.ProcessCount, "Process count");
                    Check.AreEqual(309L, web.PeakWorkingSetBytes, "Peak combined working set");
                    Check.AreEqual("web", byName[0].Group, "Sorted by CPU");
                    Check.AreEqual(2, accountant.Report(AccountingGroupByEnum.User).Count, "Two users");
                }),

                Case(Id, "PolicyAndExecutorGuardActions", "Action policy denies by default, protects system processes, and detects PID reuse", () =>
                {
                    DateTime started = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    ProcessSnapshot target = Proc(4242, "worker", started);
                    ProcessActionRequest kill = new ProcessActionRequest { HostId = "h", Pid = 4242, Action = ProcessActionKindEnum.Kill, StartTimeUtc = started };

                    Check.IsNotNull(new ProcessActionPolicy().Evaluate(kill, target), "Denied by default");
                    Check.IsNull(ProcessActionPolicy.AllowAll().Evaluate(kill, target), "Allowed by AllowAll");
                    Check.IsNotNull(ProcessActionPolicy.AllowAll().Evaluate(kill, Proc(4242, "launchd", started)), "Protected name");
                    Check.IsNotNull(ProcessActionPolicy.AllowAll().Evaluate(new ProcessActionRequest { Pid = 4242, Action = ProcessActionKindEnum.SetPriority }, target), "SetPriority needs a priority");

                    ProcessActionExecutor executor = new ProcessActionExecutor(ProcessActionPolicy.AllowAll());
                    ProcessActionResponse stale = executor.Execute(kill, new[] { Proc(4242, "worker", started.AddMinutes(5)) });
                    Check.AreEqual("Stale", stale.Result, "PID reuse is refused");
                    ProcessActionResponse denied = new ProcessActionExecutor(new ProcessActionPolicy()).Execute(kill, new[] { target });
                    Check.AreEqual("PolicyDenied", denied.Result, "Policy denial");
                }),

                Case(Id, "JsonRoundTrip", "Snapshots round-trip through the shared JSON settings with nulls omitted", () =>
                {
                    HostSnapshot s = Snapshot(DateTime.UtcNow, cpu: 12.5, procs: new List<ProcessSnapshot> { Proc(7, "x", DateTime.UtcNow, cpu: 1) });
                    string json = MonitoringJson.Serialize(s);
                    Check.IsFalse(json.Contains("\"loadAverage1\""), "Null fields are omitted");
                    Check.IsTrue(json.Contains("\"cpuPercent\":12.5"), "camelCase");
                    HostSnapshot back = MonitoringJson.Deserialize<HostSnapshot>(json);
                    Check.AreEqual(12.5, back.System.CpuPercent, "CPU");
                    Check.AreEqual(s.Processes[0].Key, back.Processes[0].Key, "Process key");
                }),

                CaseAsync(Id, "SqliteStoreWritesQueriesAndCompacts", "SqliteHistoryStore stores, buckets, rolls up, accounts, and applies retention", async ct =>
                {
                    string path = Path.Combine(Path.GetTempPath(), "perfstats-test-" + Guid.NewGuid().ToString("N") + ".db");
                    try
                    {
                        HistoryStoreSettings settings = new HistoryStoreSettings { RawRetention = TimeSpan.FromMinutes(5), TopProcessesPerMetric = 1, BackgroundProcessInterval = TimeSpan.FromSeconds(30) };
                        using (SqliteHistoryStore store = new SqliteHistoryStore(path, settings))
                        {
                            await store.InitializeAsync(ct).ConfigureAwait(false);
                            await store.UpsertHostAsync(new HostInfo { HostId = "h1", HostName = "H1", LogicalProcessors = 2 }, ct).ConfigureAwait(false);

                            DateTime t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                            for (int i = 0; i < 180; i++)
                            {
                                HostSnapshot s = Snapshot(t0.AddSeconds(i), cpu: i < 60 ? 10 : 30, procs: new List<ProcessSnapshot>
                                {
                                    Proc(1, "busy", t0, cpu: 50, ws: 1000, user: "alice"),
                                    Proc(2, "idle", t0, cpu: 0, ws: 10, user: "bob")
                                });
                                s.HostId = "h1";
                                await store.WriteAsync(s, ct).ConfigureAwait(false);
                            }

                            List<SeriesPoint> raw = await store.QuerySystemAsync("h1", MetricIds.SystemCpu, t0, t0.AddMinutes(3), 1, ct).ConfigureAwait(false);
                            Check.AreEqual(180, raw.Count, "Raw points");
                            List<SeriesPoint> perMinute = await store.QuerySystemAsync("h1", MetricIds.SystemCpu, t0, t0.AddMinutes(3), 60, ct).ConfigureAwait(false);
                            Check.AreEqual(3, perMinute.Count, "Bucketed to minutes");
                            Check.AreEqual(10.0, perMinute[0].Value, "Minute 0 average");
                            Check.AreEqual(30.0, perMinute[2].Value, "Minute 2 average");

                            List<SeriesPoint> core = await store.QuerySystemAsync("h1", "system.cpu.core.1", t0, t0.AddMinutes(3), 60, ct).ConfigureAwait(false);
                            Check.AreEqual(3, core.Count, "Per-core series stored");

                            List<SeriesPoint> idle = await store.QueryProcessAsync("h1", Proc(2, "idle", t0).Key, MetricIds.ProcessCpu, t0, t0.AddMinutes(3), 1, ct).ConfigureAwait(false);
                            Check.AreEqual(6, idle.Count, "Background process recorded every 30s");
                            List<SeriesPoint> busy = await store.QueryProcessAsync("h1", Proc(1, "busy", t0).Key, MetricIds.ProcessCpu, t0, t0.AddMinutes(3), 1, ct).ConfigureAwait(false);
                            Check.AreEqual(180, busy.Count, "Top process recorded every sample");

                            // Roll up the three completed minutes, then retention removes raw points older than five minutes.
                            await store.CompactAsync(t0.AddMinutes(3), ct).ConfigureAwait(false);
                            List<AccountingRow> accounting = await store.QueryAccountingAsync("h1", t0, t0.AddMinutes(3), AccountingGroupByEnum.Name, ct).ConfigureAwait(false);
                            AccountingRow busyRow = accounting.First(r => r.Group == "busy");
                            Check.AreEqual(180.0, busyRow.CpuSeconds, "50% of 2 cores for 180s = 180 CPU-seconds");
                            Check.AreEqual(1000L, busyRow.PeakWorkingSetBytes, "Peak working set");

                            await store.CompactAsync(t0.AddMinutes(10), ct).ConfigureAwait(false);
                            List<SeriesPoint> rawAfter = await store.QuerySystemAsync("h1", MetricIds.SystemCpu, t0, t0.AddMinutes(3), 1, ct).ConfigureAwait(false);
                            Check.AreEqual(3, rawAfter.Count, "Raw expired; query falls back to minute aggregates");

                            List<ProcessRecord> records = await store.QueryProcessesAsync("h1", t0, t0.AddMinutes(3), ct).ConfigureAwait(false);
                            Check.AreEqual(2, records.Count, "Process records");
                            List<HostRecord> hosts = await store.GetHostsAsync(ct).ConfigureAwait(false);
                            Check.AreEqual("H1", hosts.Single().Info.HostName, "Host info kept");
                        }
                    }
                    finally
                    {
                        foreach (string f in new[] { path, path + "-wal", path + "-shm" })
                        {
                            try { if (File.Exists(f)) File.Delete(f); } catch { }
                        }
                    }
                }),
            };

            return new TestSuiteDescriptor(Id, "Monitoring Layer", cases);
        }

        private static HostSnapshot Snapshot(DateTime ts, double cpu = 0, List<ProcessSnapshot> procs = null)
        {
            HostSnapshot s = new HostSnapshot { HostId = "h", TimestampUtc = ts, IntervalSeconds = 1 };
            s.System.CpuPercent = cpu;
            s.System.PerCoreCpuPercent = new double[] { cpu, cpu };
            s.System.MemoryTotalBytes = 1000;
            s.System.MemoryUsedBytes = 400;
            s.Processes = procs ?? new List<ProcessSnapshot>();
            return s;
        }

        private static ProcessSnapshot Proc(int pid, string name, DateTime started, double cpu = 0, long ws = 0, double read = 0, string user = "u")
        {
            return new ProcessSnapshot { Pid = pid, Name = name, StartTimeUtc = started, CpuPercent = cpu, WorkingSetBytes = ws, DiskReadBytesPerSecond = read, DiskWriteBytesPerSecond = 0, User = user };
        }
    }
}
