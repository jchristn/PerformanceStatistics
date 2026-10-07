using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PerformanceStatistics.Monitoring;
using TaskManager.Agent;
using Touchstone.Cli;
using Touchstone.Core;

namespace Test.Server
{
    /// <summary>
    /// End-to-end tests: the real server on a random port with the real agent reporting to it.
    /// </summary>
    public static class Program
    {
        private const string Id = "Server";
        private const string AdminPassword = "admin-pass";
        private const string ViewerPassword = "viewer-pass";
        private const string AgentKey = "agent-key";
        private const string HostId = "it-host";

        public static async Task<int> Main(string[] args)
        {
            string resultsPath = args != null && args.Length > 0 ? args[0] : null;
            string db = Path.Combine(Path.GetTempPath(), "taskmgr-it-" + Guid.NewGuid().ToString("N") + ".db");
            WebApplication server = TaskManager.Server.Program.Build(new[] { "--urls", "http://127.0.0.1:0" }, b =>
            {
                b.Logging.ClearProviders();
                b.Configuration.AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "TASKMGR:ADMINPASSWORD", AdminPassword },
                    { "TASKMGR:VIEWERPASSWORD", ViewerPassword },
                    { "TASKMGR:AGENTKEY", AgentKey },
                    { "TASKMGR:STORAGE", db }
                });
            });
            await server.StartAsync().ConfigureAwait(false);
            string url = server.Urls.First();

            AgentWorker agent = new AgentWorker(
                new AgentSettings { Server = url, AgentKey = AgentKey, HostId = HostId, IntervalSeconds = 0.5, AllowedActions = "Kill,Suspend,Resume,SetPriority" },
                NullLogger<AgentWorker>.Instance);
            await agent.StartAsync(CancellationToken.None).ConfigureAwait(false);

            int code;
            try
            {
                code = await ConsoleRunner.RunAsync(new List<TestSuiteDescriptor> { Build(url, server) }, sink: null, resultsPath: resultsPath, cancellationToken: CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                await agent.StopAsync(CancellationToken.None).ConfigureAwait(false);
                await server.StopAsync().ConfigureAwait(false);
                await server.DisposeAsync().ConfigureAwait(false);
                foreach (string f in new[] { db, db + "-wal", db + "-shm" }) { try { File.Delete(f); } catch { } }
            }

            return code;
        }

        private static TestSuiteDescriptor Build(string url, WebApplication server)
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Case("RequiresLogin", "API calls without a session are rejected; wrong passwords fail", async ct =>
                {
                    using (HttpClient http = new HttpClient { BaseAddress = new Uri(url) })
                    {
                        Must((await http.GetAsync("/api/v1/hosts", ct)).StatusCode == HttpStatusCode.Unauthorized, "401 without a token");
                        Must((await http.GetAsync("/health", ct)).IsSuccessStatusCode, "Health is public");
                    }

                    using (MonitoringClient client = new MonitoringClient(url))
                    {
                        bool failed = false;
                        try { await client.LoginAsync("admin", "nope", ct); } catch (HttpRequestException) { failed = true; }
                        Must(failed, "Wrong password rejected");
                    }
                }),

                Case("AgentKeyRequired", "Agent endpoints reject a wrong key", async ct =>
                {
                    using (HttpClient http = new HttpClient { BaseAddress = new Uri(url) })
                    {
                        http.DefaultRequestHeaders.Add("X-Agent-Key", "wrong");
                        HttpResponseMessage r = await http.PostAsync("/api/v1/agent/ingest", new StringContent("[]", Encoding.UTF8, "application/json"), ct);
                        Must(r.StatusCode == HttpStatusCode.Unauthorized, "401 with a wrong agent key");
                    }
                }),

                Case("AgentAppearsOnline", "The agent's host appears online with its facts and allowed actions", async ct =>
                {
                    using (MonitoringClient client = await Admin(url, ct))
                    {
                        HostStatus host = await WaitForHost(client, ct);
                        Must(host.Online, "Online");
                        Must(host.Info.LogicalProcessors == Environment.ProcessorCount, "Host facts");
                        Must(host.AllowedActions.Contains(ProcessActionKindEnum.Kill) && !host.AllowedActions.Contains(ProcessActionKindEnum.KillTree), "Allowed actions");
                        Must(host.CpuPercent.HasValue, "Latest CPU in the host list");
                    }
                }),

                Case("SnapshotAndProcesses", "Snapshot and server-side sorted process list", async ct =>
                {
                    using (MonitoringClient client = await Admin(url, ct))
                    using (HttpClient http = new HttpClient { BaseAddress = new Uri(url) })
                    {
                        await WaitForHost(client, ct);
                        HostSnapshot s = await client.GetSnapshotAsync(HostId, ct);
                        Must(s.Processes.Any(p => p.Pid == Environment.ProcessId), "This process is in the snapshot");
                        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", client.Token);
                        string json = await http.GetStringAsync("/api/v1/hosts/" + HostId + "/processes?sort=process.memory.workingset&top=5", ct);
                        List<ProcessSnapshot> top = MonitoringJson.Deserialize<List<ProcessSnapshot>>(json);
                        Must(top.Count == 5, "Top 5");
                        for (int i = 1; i < top.Count; i++) Must((top[i - 1].WorkingSetBytes ?? 0) >= (top[i].WorkingSetBytes ?? 0), "Sorted by memory, descending");
                        Must((await http.GetAsync("/api/v1/hosts/" + HostId + "/processes?sort=bogus", ct)).StatusCode == HttpStatusCode.BadRequest, "Unknown sort metric is a 400");
                    }
                }),

                Case("StreamDelivers", "The WebSocket stream delivers live snapshots", async ct =>
                {
                    using (MonitoringClient client = await Admin(url, ct))
                    using (CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        await WaitForHost(client, ct);
                        int count = 0;
                        cts.CancelAfter(TimeSpan.FromSeconds(10));
                        Task stream = client.StreamAsync(HostId, s => { if (Interlocked.Increment(ref count) >= 3) cts.Cancel(); }, cts.Token);
                        await stream;
                        Must(count >= 3, "At least three snapshots, got " + count);
                    }
                }),

                Case("HistoryAndCatalog", "History returns stored points and the catalog is served", async ct =>
                {
                    using (MonitoringClient client = await Admin(url, ct))
                    {
                        await WaitForHost(client, ct);
                        await Task.Delay(1500, ct);
                        List<SeriesPoint> cpu = await client.GetHistoryAsync(HostId, MetricIds.SystemCpu, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddSeconds(5), 1, ct);
                        Must(cpu.Count >= 2, "CPU history points: " + cpu.Count);
                        List<SeriesPoint> core = await client.GetHistoryAsync(HostId, "system.cpu.core.0", DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddSeconds(5), 1, ct);
                        Must(core.Count >= 2, "Per-core history");
                        List<MetricDefinition> catalog = await client.GetCatalogAsync(ct);
                        Must(catalog.Any(m => m.Id == MetricIds.ProcessCpu && m.Availability.ContainsKey("Mac")), "Catalog with availability");
                    }
                }),

                Case("AccountingAfterCompaction", "Accounting reports consumers once minutes are rolled up", async ct =>
                {
                    using (MonitoringClient client = await Admin(url, ct))
                    {
                        await WaitForHost(client, ct);
                        IHistoryStore store = server.Services.GetRequiredService<IHistoryStore>();
                        await store.CompactAsync(DateTime.UtcNow.AddMinutes(2), ct);
                        List<AccountingRow> rows = await client.GetAccountingAsync(HostId, DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(2), AccountingGroupByEnum.Name, ct);
                        Must(rows.Count > 0, "Accounting rows");
                    }
                }),

                Case("ActionsEndToEnd", "Admin suspends, resumes, and kills a child process through the agent", async ct =>
                {
                    using (MonitoringClient client = await Admin(url, ct))
                    using (Process child = StartSleeper())
                    {
                        try
                        {
                            await WaitForHost(client, ct);
                            Must((await Act(client, child.Id, ProcessActionKindEnum.Suspend, ct)).Succeeded, "Suspend");
                            Must((await Act(client, child.Id, ProcessActionKindEnum.Resume, ct)).Succeeded, "Resume");
                            ProcessActionResponse kill = await Act(client, child.Id, ProcessActionKindEnum.Kill, ct);
                            Must(kill.Succeeded, "Kill: " + kill.Result + " " + kill.Message);
                            Must(child.WaitForExit(10000), "Child exited");

                            using (HttpClient http = new HttpClient { BaseAddress = new Uri(url) })
                            {
                                http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", client.Token);
                                string audit = await http.GetStringAsync("/api/v1/hosts/" + HostId + "/audit", ct);
                                Must(audit.Contains("Requested") && audit.Contains("Success"), "Audit trail");
                            }
                        }
                        finally
                        {
                            try { if (!child.HasExited) child.Kill(); } catch { }
                        }
                    }
                }),

                Case("ActionsDenied", "Actions the agent does not allow, and actions by viewers, are refused", async ct =>
                {
                    using (MonitoringClient admin = await Admin(url, ct))
                    using (MonitoringClient viewer = new MonitoringClient(url))
                    {
                        await WaitForHost(admin, ct);
                        await viewer.LoginAsync("viewer", ViewerPassword, ct);
                        Must(await Forbidden(() => admin.RequestActionAsync(new ProcessActionRequest { HostId = HostId, Pid = 99999, Action = ProcessActionKindEnum.KillTree }, ct)), "KillTree is not allowed by the agent");
                        Must(await Forbidden(() => viewer.RequestActionAsync(new ProcessActionRequest { HostId = HostId, Pid = 99999, Action = ProcessActionKindEnum.Kill }, ct)), "Viewers cannot act");
                        Must((await viewer.GetHostsAsync(ct)).Count == 1, "Viewers can read");
                    }
                }),
            };

            return new TestSuiteDescriptor(Id, "Server and Agent (end to end)", cases);
        }

        private static TestCaseDescriptor Case(string caseId, string name, Func<CancellationToken, Task> body)
        {
            return new TestCaseDescriptor(Id, caseId, name, body);
        }

        private static void Must(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static async Task<MonitoringClient> Admin(string url, CancellationToken ct)
        {
            MonitoringClient client = new MonitoringClient(url);
            await client.LoginAsync("admin", AdminPassword, ct);
            return client;
        }

        private static async Task<HostStatus> WaitForHost(MonitoringClient client, CancellationToken ct)
        {
            for (int i = 0; i < 100; i++)
            {
                HostStatus host = (await client.GetHostsAsync(ct)).FirstOrDefault(h => h.Info.HostId == HostId);
                if (host != null && host.Online && host.CpuPercent.HasValue) return host;
                await Task.Delay(100, ct);
            }

            throw new Exception("The agent's host did not appear online.");
        }

        private static async Task<ProcessActionResponse> Act(MonitoringClient client, int pid, ProcessActionKindEnum action, CancellationToken ct)
        {
            ProcessActionRequest request = new ProcessActionRequest { HostId = HostId, Pid = pid, Action = action };
            ProcessActionResponse r = await client.RequestActionAsync(request, ct);
            for (int i = 0; i < 100 && !r.CompletedUtc.HasValue; i++)
            {
                await Task.Delay(100, ct);
                r = await client.GetActionAsync(HostId, request.Id, ct);
            }

            return r;
        }

        private static async Task<bool> Forbidden(Func<Task> call)
        {
            try
            {
                await call();
                return false;
            }
            catch (HttpRequestException e)
            {
                return e.StatusCode == HttpStatusCode.Forbidden;
            }
        }

        private static Process StartSleeper()
        {
            ProcessStartInfo psi = OperatingSystem.IsWindows() ? new ProcessStartInfo("ping", "-n 120 127.0.0.1") : new ProcessStartInfo("/bin/sleep", "120");
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            return Process.Start(psi);
        }
    }
}
