using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TaskManager.Agent
{
    /// <summary>
    /// TaskManager agent: runs on each monitored machine (Windows Service, launchd daemon, systemd unit, or a
    /// container with the host's /proc and /sys mounted) and reports to a TaskManager server.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
            AgentSettings settings = new AgentSettings();
            builder.Configuration.GetSection("TASKMGR").Bind(settings);

            if (String.IsNullOrWhiteSpace(settings.Server) || String.IsNullOrWhiteSpace(settings.AgentKey))
            {
                Console.Error.WriteLine("Set TASKMGR__SERVER (for example http://monitor:8080) and TASKMGR__AGENTKEY.");
                Console.Error.WriteLine("Optional: TASKMGR__HOSTID, TASKMGR__INTERVALSECONDS, TASKMGR__ALLOWEDACTIONS (Kill,KillTree,SetPriority,Suspend,Resume), TASKMGR__ALLOWEDPROCESSUSERS, TASKMGR__BUFFERSIZE.");
                return 2;
            }

            try
            {
                settings.ToPolicy();
            }
            catch (InvalidOperationException e)
            {
                Console.Error.WriteLine(e.Message);
                return 2;
            }

            builder.Services.AddWindowsService(o => o.ServiceName = "TaskManager Agent");
            builder.Services.AddSystemd();
            builder.Services.AddSingleton(settings);
            builder.Services.AddHostedService<AgentWorker>();
            builder.Build().Run();
            return 0;
        }
    }
}
