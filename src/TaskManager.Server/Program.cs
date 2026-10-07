using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PerformanceStatistics.Monitoring;

namespace TaskManager.Server
{
    /// <summary>
    /// TaskManager server: receives snapshots from agents, stores history, and serves the API and live stream
    /// used by the web app and the TUI's remote mode.
    /// </summary>
    public static class Program
    {
        #region Public-Members

        /// <summary>
        /// HttpContext item key for the authenticated session.
        /// </summary>
        public const string SessionItem = "taskmgr.session";

        #endregion

        #region Public-Methods

        public static async Task Main(string[] args)
        {
            WebApplication app = Build(args);
            await app.RunAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Build the application (used by tests with a custom configuration).
        /// </summary>
        public static WebApplication Build(string[] args, Action<WebApplicationBuilder> configure = null)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            configure?.Invoke(builder);

            ServerSettings settings = new ServerSettings();
            builder.Configuration.GetSection("TASKMGR").Bind(settings);
            if (String.IsNullOrEmpty(settings.AdminPassword))
                throw new InvalidOperationException("Set TASKMGR__ADMINPASSWORD (the admin password).");
            if (settings.GetAgentKeys().Count == 0)
                throw new InvalidOperationException("Set TASKMGR__AGENTKEY (the key agents use to send data).");

            string directory = Path.GetDirectoryName(Path.GetFullPath(settings.Storage));
            if (!String.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            builder.Services.AddSingleton(settings);
            builder.Services.AddSingleton<SessionService>();
            builder.Services.AddSingleton<HostRegistry>();
            builder.Services.AddSingleton<IHistoryStore>(sp => new SqliteHistoryStore(settings.Storage, settings.ToHistorySettings()));
            builder.Services.AddSingleton(sp => new AuditLog(settings.Storage));
            builder.Services.AddSingleton<StartupService>();
            builder.Services.AddHostedService(sp => sp.GetRequiredService<StartupService>());
            builder.Services.AddRequestDecompression();
            builder.Services.AddResponseCompression(o => o.EnableForHttps = true);
            builder.Services.ConfigureHttpJsonOptions(o =>
            {
                o.SerializerOptions.PropertyNamingPolicy = MonitoringJson.Options.PropertyNamingPolicy;
                o.SerializerOptions.DefaultIgnoreCondition = MonitoringJson.Options.DefaultIgnoreCondition;
                foreach (System.Text.Json.Serialization.JsonConverter c in MonitoringJson.Options.Converters) o.SerializerOptions.Converters.Add(c);
            });
            if (!String.IsNullOrWhiteSpace(settings.CorsOrigins))
            {
                string[] origins = settings.CorsOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
            }

            WebApplication app = builder.Build();
            app.UseRequestDecompression();
            app.UseResponseCompression();
            if (!String.IsNullOrWhiteSpace(settings.CorsOrigins)) app.UseCors();
            app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });

            MapPublic(app);
            MapUser(app.MapGroup("/api/v1").AddEndpointFilter(RequireUser));
            MapAgent(app.MapGroup("/api/v1/agent").AddEndpointFilter(RequireAgent));
            app.Map("/api/v1/stream", StreamAsync);
            return app;
        }

        #endregion

        #region Private-Methods

        private static void MapPublic(WebApplication app)
        {
            app.MapGet("/health", () => Results.Ok(new { status = "ok", timeUtc = DateTime.UtcNow }));

            app.MapPost("/api/v1/auth/login", (LoginRequest login, SessionService sessions) =>
            {
                LoginResponse session = login == null ? null : sessions.Login(login.Username, login.Password);
                return session == null ? Results.Json(new { error = "Invalid user name or password." }, statusCode: 401) : Results.Ok(session);
            });
        }

        private static void MapUser(RouteGroupBuilder api)
        {
            api.MapPost("/auth/logout", (HttpContext ctx, SessionService sessions) =>
            {
                sessions.Logout(BearerToken(ctx));
                return Results.NoContent();
            });

            api.MapGet("/me", (HttpContext ctx) =>
            {
                SessionService.Session s = (SessionService.Session)ctx.Items[SessionItem];
                return Results.Ok(new { username = s.Username, isAdmin = s.IsAdmin, expiresUtc = s.ExpiresUtc });
            });

            api.MapGet("/catalog", () => Results.Ok(MetricCatalog.All));

            api.MapGet("/hosts", (HostRegistry hosts) => Results.Ok(hosts.Statuses()));

            api.MapGet("/hosts/{id}", (string id, HostRegistry hosts) =>
            {
                HostStatus status = hosts.Statuses().FirstOrDefault(h => String.Equals(h.Info.HostId, id, StringComparison.OrdinalIgnoreCase));
                return status == null ? NotFound("Unknown host.") : Results.Ok(status);
            });

            api.MapGet("/hosts/{id}/snapshot", (string id, bool? processes, HostRegistry hosts) =>
            {
                HostSnapshot latest = hosts.Latest(id);
                if (latest == null) return NotFound("No data from this host yet.");
                if (processes == false) latest = WithoutProcesses(latest);
                return Results.Ok(latest);
            });

            api.MapGet("/hosts/{id}/processes", (string id, string sort, bool? desc, string filter, int? top, HostRegistry hosts) =>
            {
                HostSnapshot latest = hosts.Latest(id);
                if (latest == null) return NotFound("No data from this host yet.");
                IEnumerable<ProcessSnapshot> rows = Filter(latest.Processes, filter);
                string metric = String.IsNullOrEmpty(sort) ? MetricIds.ProcessCpu : sort;
                MetricDefinition m = MetricCatalog.Get(metric);
                if (m == null || m.Scope != MetricScopeEnum.Process) return BadRequest("Unknown process metric: " + metric);
                bool descending = desc ?? (m.Unit != MetricUnitEnum.Text);
                rows = m.Unit == MetricUnitEnum.Text
                    ? (descending ? rows.OrderByDescending(p => MetricCatalog.ProcessDisplay(metric, p), StringComparer.OrdinalIgnoreCase) : rows.OrderBy(p => MetricCatalog.ProcessDisplay(metric, p), StringComparer.OrdinalIgnoreCase))
                    : (descending ? rows.OrderByDescending(p => MetricCatalog.ProcessValue(metric, p) ?? -1) : rows.OrderBy(p => MetricCatalog.ProcessValue(metric, p) ?? -1));
                if (top.HasValue && top.Value > 0) rows = rows.Take(top.Value);
                return Results.Ok(rows.ToList());
            });

            api.MapGet("/hosts/{id}/process-records", async (string id, string from, string to, IHistoryStore store, CancellationToken ct) =>
            {
                DateTime f, t;
                string error = ParseRange(from, to, out f, out t);
                if (error != null) return BadRequest(error);
                return Results.Ok(await store.QueryProcessesAsync(id, f, t, ct).ConfigureAwait(false));
            });

            api.MapGet("/hosts/{id}/history", async (string id, string metric, string from, string to, int? step, IHistoryStore store, CancellationToken ct) =>
            {
                DateTime f, t;
                string error = ParseRange(from, to, out f, out t);
                if (error != null) return BadRequest(error);
                if (MetricCatalog.Get(metric) == null) return BadRequest("Unknown metric: " + metric);
                return Results.Ok(await store.QuerySystemAsync(id, metric, f, t, Step(step, f, t), ct).ConfigureAwait(false));
            });

            api.MapGet("/hosts/{id}/processes/{key}/history", async (string id, string key, string metric, string from, string to, int? step, IHistoryStore store, CancellationToken ct) =>
            {
                DateTime f, t;
                string error = ParseRange(from, to, out f, out t);
                if (error != null) return BadRequest(error);
                if (MetricCatalog.Get(metric) == null) return BadRequest("Unknown metric: " + metric);
                return Results.Ok(await store.QueryProcessAsync(id, key, metric, f, t, Step(step, f, t), ct).ConfigureAwait(false));
            });

            api.MapGet("/hosts/{id}/interfaces/{name}/history", async (string id, string name, string metric, string from, string to, int? step, IHistoryStore store, CancellationToken ct) =>
            {
                DateTime f, t;
                string error = ParseRange(from, to, out f, out t);
                if (error != null) return BadRequest(error);
                if (MetricCatalog.Get(metric) == null) return BadRequest("Unknown metric: " + metric);
                return Results.Ok(await store.QueryInterfaceAsync(id, name, metric, f, t, Step(step, f, t), ct).ConfigureAwait(false));
            });

            api.MapGet("/hosts/{id}/accounting", async (string id, string from, string to, string groupBy, IHistoryStore store, CancellationToken ct) =>
            {
                DateTime f, t;
                string error = ParseRange(from, to, out f, out t);
                if (error != null) return BadRequest(error);
                AccountingGroupByEnum g = String.Equals(groupBy, "user", StringComparison.OrdinalIgnoreCase) ? AccountingGroupByEnum.User : AccountingGroupByEnum.Name;
                return Results.Ok(await store.QueryAccountingAsync(id, f, t, g, ct).ConfigureAwait(false));
            });

            api.MapPost("/hosts/{id}/actions", async (HttpContext ctx, string id, ProcessActionRequest request, HostRegistry hosts, AuditLog audit, CancellationToken ct) =>
            {
                SessionService.Session s = (SessionService.Session)ctx.Items[SessionItem];
                if (!s.IsAdmin) return Results.Json(new { error = "Only administrators can act on processes." }, statusCode: 403);
                if (request == null || request.Pid <= 0) return BadRequest("A process ID is required.");
                if (!hosts.Exists(id)) return NotFound("Unknown host.");
                if (!hosts.AllowedActions(id).Contains(request.Action))
                    return Results.Json(new { error = "The agent on " + id + " does not allow " + request.Action + ". Enable it with TASKMGR__ALLOWEDACTIONS on that host." }, statusCode: 403);

                request.HostId = id;
                request.RequestedBy = s.Username;
                request.RequestedUtc = DateTime.UtcNow;
                if (String.IsNullOrEmpty(request.Id)) request.Id = Guid.NewGuid().ToString("N");
                ProcessActionResponse pending = hosts.Enqueue(request);
                await audit.WriteAsync(request.Id, id, request.Pid, request.Action.ToString(), s.Username, "Requested", request.Priority?.ToString(), ct).ConfigureAwait(false);
                return Results.Accepted("/api/v1/hosts/" + id + "/actions/" + request.Id, pending);
            });

            api.MapGet("/hosts/{id}/actions/{requestId}", (string id, string requestId, HostRegistry hosts) =>
            {
                ProcessActionResponse r = hosts.Response(id, requestId);
                return r == null ? NotFound("Unknown request.") : Results.Ok(r);
            });

            api.MapGet("/hosts/{id}/audit", async (string id, int? limit, AuditLog audit, CancellationToken ct) =>
                Results.Ok(await audit.ReadAsync(id, limit ?? 100, ct).ConfigureAwait(false)));
        }

        private static void MapAgent(RouteGroupBuilder agent)
        {
            agent.MapPost("/hello", async (AgentHello hello, HostRegistry hosts, IHistoryStore store, StartupService startup, CancellationToken ct) =>
            {
                if (hello?.Info == null || String.IsNullOrWhiteSpace(hello.Info.HostId)) return BadRequest("Host info with a hostId is required.");
                await startup.Ready.ConfigureAwait(false);
                hosts.Hello(hello);
                await store.UpsertHostAsync(hello.Info, ct).ConfigureAwait(false);
                return Results.NoContent();
            });

            agent.MapPost("/ingest", async (List<HostSnapshot> snapshots, HostRegistry hosts, IHistoryStore store, StartupService startup, CancellationToken ct) =>
            {
                if (snapshots == null || snapshots.Count == 0) return BadRequest("No snapshots.");
                await startup.Ready.ConfigureAwait(false);
                foreach (HostSnapshot s in snapshots.OrderBy(s => s.TimestampUtc))
                {
                    if (String.IsNullOrWhiteSpace(s.HostId)) continue;
                    await store.WriteAsync(s, ct).ConfigureAwait(false);
                    hosts.Publish(s);
                }

                return Results.NoContent();
            });

            agent.MapGet("/actions", (string host, HostRegistry hosts) => Results.Ok(hosts.TakePending(host)));

            agent.MapPost("/actions/results", async (string host, List<ProcessActionResponse> results, HostRegistry hosts, AuditLog audit, CancellationToken ct) =>
            {
                if (results == null) return BadRequest("No results.");
                hosts.Complete(host, results);
                foreach (ProcessActionResponse r in results)
                    await audit.WriteAsync(r.RequestId, host, r.Pid, r.Action.ToString(), "agent", r.Result, r.Message, ct).ConfigureAwait(false);
                return Results.NoContent();
            });
        }

        private static async Task StreamAsync(HttpContext ctx)
        {
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = 400;
                return;
            }

            SessionService sessions = ctx.RequestServices.GetRequiredService<SessionService>();
            string token = ctx.Request.Query["access_token"].FirstOrDefault() ?? BearerToken(ctx);
            if (sessions.Validate(token) == null)
            {
                ctx.Response.StatusCode = 401;
                return;
            }

            string hostId = ctx.Request.Query["host"].FirstOrDefault();
            HostRegistry hosts = ctx.RequestServices.GetRequiredService<HostRegistry>();
            if (String.IsNullOrEmpty(hostId) || !hosts.Exists(hostId))
            {
                ctx.Response.StatusCode = 404;
                return;
            }

            using (WebSocket ws = await ctx.WebSockets.AcceptWebSocketAsync(new WebSocketAcceptContext { DangerousEnableCompression = true }).ConfigureAwait(false))
            {
                Channel<HostSnapshot> channel = hosts.Subscribe(hostId);
                CancellationToken ct = ctx.RequestAborted;
                Task receive = DrainAsync(ws, ct);
                try
                {
                    HostSnapshot latest = hosts.Latest(hostId);
                    if (latest != null) await SendAsync(ws, latest, ct).ConfigureAwait(false);

                    while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                    {
                        Task<bool> ready = channel.Reader.WaitToReadAsync(ct).AsTask();
                        Task done = await Task.WhenAny(ready, receive).ConfigureAwait(false);
                        if (done == receive || !await ready.ConfigureAwait(false)) break;

                        HostSnapshot next = null;
                        HostSnapshot item;
                        while (channel.Reader.TryRead(out item)) next = item;
                        if (next != null) await SendAsync(ws, next, ct).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (WebSocketException)
                {
                }
                finally
                {
                    hosts.Unsubscribe(hostId, channel);
                    if (ws.State == WebSocketState.Open)
                    {
                        try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None).ConfigureAwait(false); }
                        catch { }
                    }
                }
            }
        }

        private static async Task DrainAsync(WebSocket ws, CancellationToken ct)
        {
            byte[] buffer = new byte[1024];
            try
            {
                while (ws.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult r = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);
                    if (r.MessageType == WebSocketMessageType.Close) return;
                }
            }
            catch
            {
            }
        }

        private static Task SendAsync(WebSocket ws, HostSnapshot snapshot, CancellationToken ct)
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, MonitoringJson.Options);
            return ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
        }

        private static async ValueTask<object> RequireUser(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            HttpContext ctx = context.HttpContext;
            SessionService sessions = ctx.RequestServices.GetRequiredService<SessionService>();
            SessionService.Session session = sessions.Validate(BearerToken(ctx));
            if (session == null) return Results.Json(new { error = "Log in first." }, statusCode: 401);
            ctx.Items[SessionItem] = session;
            return await next(context).ConfigureAwait(false);
        }

        private static async ValueTask<object> RequireAgent(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            HttpContext ctx = context.HttpContext;
            ServerSettings settings = ctx.RequestServices.GetRequiredService<ServerSettings>();
            string key = ctx.Request.Headers["X-Agent-Key"].FirstOrDefault();
            if (String.IsNullOrEmpty(key) || !settings.GetAgentKeys().Contains(key)) return Results.Json(new { error = "Invalid agent key." }, statusCode: 401);
            return await next(context).ConfigureAwait(false);
        }

        private static string BearerToken(HttpContext ctx)
        {
            string header = ctx.Request.Headers.Authorization.FirstOrDefault();
            if (header != null && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return header.Substring(7).Trim();
            return null;
        }

        private static IEnumerable<ProcessSnapshot> Filter(IEnumerable<ProcessSnapshot> processes, string filter)
        {
            if (String.IsNullOrWhiteSpace(filter)) return processes;
            string f = filter.Trim();
            return processes.Where(p =>
                p.Pid.ToString(CultureInfo.InvariantCulture) == f
                || Contains(p.Name, f) || Contains(p.User, f) || Contains(p.ExecutablePath, f) || Contains(p.CommandLine, f));
        }

        private static bool Contains(string value, string filter)
        {
            return !String.IsNullOrEmpty(value) && value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ParseRange(string from, string to, out DateTime fromUtc, out DateTime toUtc)
        {
            toUtc = DateTime.UtcNow;
            fromUtc = toUtc.AddHours(-1);
            if (!String.IsNullOrEmpty(to) && !DateTime.TryParse(to, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out toUtc))
                return "Invalid 'to' time.";
            if (!String.IsNullOrEmpty(from) && !DateTime.TryParse(from, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out fromUtc))
                return "Invalid 'from' time.";
            if (String.IsNullOrEmpty(from)) fromUtc = toUtc.AddHours(-1);
            if (fromUtc >= toUtc) return "'from' must be before 'to'.";
            return null;
        }

        private static int Step(int? step, DateTime from, DateTime to)
        {
            if (step.HasValue && step.Value > 0) return step.Value;
            return Math.Max(1, (int)Math.Ceiling((to - from).TotalSeconds / 300.0));
        }

        private static HostSnapshot WithoutProcesses(HostSnapshot s)
        {
            return new HostSnapshot { HostId = s.HostId, TimestampUtc = s.TimestampUtc, IntervalSeconds = s.IntervalSeconds, System = s.System, Network = s.Network, Volumes = s.Volumes, TcpConnections = s.TcpConnections };
        }

        private static IResult NotFound(string message)
        {
            return Results.Json(new { error = message }, statusCode: 404);
        }

        private static IResult BadRequest(string message)
        {
            return Results.Json(new { error = message }, statusCode: 400);
        }

        #endregion
    }
}
