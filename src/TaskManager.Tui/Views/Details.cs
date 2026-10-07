using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PerformanceStatistics.Monitoring;

namespace TaskManager.Tui
{
    /// <summary>
    /// Builds the content of the details modals. Every builder reads the latest state, so modals stay live.
    /// </summary>
    internal static class Details
    {
        #region Private-Members

        private static readonly Dictionary<int, string> _WellKnownPorts = new Dictionary<int, string>
        {
            { 20, "FTP data" }, { 21, "FTP" }, { 22, "SSH" }, { 23, "Telnet" }, { 25, "SMTP" }, { 53, "DNS" }, { 67, "DHCP" },
            { 80, "HTTP" }, { 110, "POP3" }, { 123, "NTP" }, { 143, "IMAP" }, { 389, "LDAP" }, { 443, "HTTPS" }, { 445, "SMB" },
            { 465, "SMTPS" }, { 587, "SMTP submission" }, { 636, "LDAPS" }, { 993, "IMAPS" }, { 995, "POP3S" }, { 1433, "SQL Server" },
            { 1521, "Oracle" }, { 2049, "NFS" }, { 2375, "Docker" }, { 2376, "Docker (TLS)" }, { 3000, "dev web" }, { 3306, "MySQL" },
            { 3389, "RDP" }, { 5000, "dev web" }, { 5432, "PostgreSQL" }, { 5672, "AMQP" }, { 5900, "VNC" }, { 6379, "Redis" },
            { 6443, "Kubernetes API" }, { 8080, "HTTP (alt)" }, { 8443, "HTTPS (alt)" }, { 9000, "dev web" }, { 9092, "Kafka" },
            { 9200, "Elasticsearch" }, { 11211, "Memcached" }, { 27017, "MongoDB" }
        };

        #endregion

        #region Public-Methods

        public static DetailsContent Process(TaskManagerState state, string key)
        {
            DetailsContent c = new DetailsContent();
            ProcessSnapshot p = state.Latest?.Processes.FirstOrDefault(x => x.Key == key);
            bool exited = p == null;
            if (p == null)
            {
                KeyValuePair<ProcessSnapshot, DateTime> known = state.History.GetKnownProcesses().FirstOrDefault(e => e.Key.Key == key);
                p = known.Key;
                if (p == null) return c.Row("Process", "No longer available.");
                c.Row("Status", "Exited (last seen " + known.Value.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture) + ")");
            }

            c.Section("Identity");
            Add(c, state, p, MetricIds.ProcessName);
            Add(c, state, p, MetricIds.ProcessPid);
            string parent = p.ParentPid.HasValue
                ? p.ParentPid.Value.ToString(CultureInfo.InvariantCulture) + Parent(state, p.ParentPid.Value)
                : NotAvailable(state, MetricIds.ProcessParentPid);
            c.Row("Parent process", parent);
            Add(c, state, p, MetricIds.ProcessUser);
            Add(c, state, p, MetricIds.ProcessState);
            Add(c, state, p, MetricIds.ProcessPriority);
            string started = MetricCatalog.ProcessDisplay(MetricIds.ProcessStart, p);
            if (p.StartTimeUtc.HasValue) started += "  (running " + MetricFormatter.Duration((DateTime.UtcNow - p.StartTimeUtc.Value).TotalSeconds) + ")";
            c.Row("Start time", started);
            Add(c, state, p, MetricIds.ProcessPath);
            Add(c, state, p, MetricIds.ProcessCommandLine);
            int children = state.Latest?.Processes.Count(x => x.ParentPid == p.Pid) ?? 0;
            c.Row("Child processes", children.ToString(CultureInfo.InvariantCulture));

            c.Section("Usage");
            foreach (string id in new[] { MetricIds.ProcessCpu, MetricIds.ProcessCpuTime, MetricIds.ProcessWorkingSet, MetricIds.ProcessPrivate,
                MetricIds.ProcessVirtual, MetricIds.ProcessDiskRead, MetricIds.ProcessDiskWrite, MetricIds.ProcessDiskReadTotal,
                MetricIds.ProcessDiskWriteTotal, MetricIds.ProcessHandles, MetricIds.ProcessThreads })
                Add(c, state, p, id);

            if (!exited)
            {
                HistoryBuffer h = state.History;
                c.Charts.Add(state.Chart("CPU %", MetricUnitEnum.Percent, null, ("CPU", Charts.Cpu, h.GetProcessSeries(key, MetricIds.ProcessCpu))));
                c.Charts.Add(state.Chart("Memory (resident)", MetricUnitEnum.Bytes, null, ("Memory", Charts.Memory, h.GetProcessSeries(key, MetricIds.ProcessWorkingSet))));
                c.Charts.Add(state.Chart("Disk I/O", MetricUnitEnum.BytesPerSecond, null,
                    ("Read", Charts.Read, h.GetProcessSeries(key, MetricIds.ProcessDiskRead)),
                    ("Write", Charts.Write, h.GetProcessSeries(key, MetricIds.ProcessDiskWrite))));
            }

            return c;
        }

        public static DetailsContent Interface(TaskManagerState state, string name)
        {
            DetailsContent c = new DetailsContent();
            NetworkInterfaceSnapshot n = state.Latest?.Network.FirstOrDefault(x => x.Name == name);
            if (n == null) return c.Row("Interface", name + " is no longer up.");

            c.Section("Interface");
            c.Row("Name", n.Name);
            c.Row("Description", Or(n.Description));
            c.Row("Type", Or(n.InterfaceType));
            c.Row("Status", "Up");
            c.Row("Link speed", n.SpeedBitsPerSecond.HasValue ? MetricFormatter.Number(n.SpeedBitsPerSecond.Value / 1e6) + " Mb/s" : MetricFormatter.NotAvailable);
            c.Row("MAC address", Or(n.MacAddress));

            c.Section("Addresses");
            List<string> addresses = n.Addresses ?? new List<string>();
            if (addresses.Count == 0) c.Row("Addresses", "None assigned");
            foreach (string a in addresses) c.Row(a.Contains(":") ? "IPv6" : "IPv4", a);

            c.Section("Throughput");
            c.Row("Received", MetricFormatter.Format(n.ReceivedBytesPerSecond, MetricUnitEnum.BytesPerSecond));
            c.Row("Sent", MetricFormatter.Format(n.SentBytesPerSecond, MetricUnitEnum.BytesPerSecond));
            c.Row("Packets received", MetricFormatter.Format(n.ReceivedPacketsPerSecond, MetricUnitEnum.PerSecond));
            c.Row("Packets sent", MetricFormatter.Format(n.SentPacketsPerSecond, MetricUnitEnum.PerSecond));
            int connections = state.Latest.TcpConnections.Count(t => addresses.Any(a => SameHost(t.Local, a)));
            c.Row("TCP connections", connections.ToString(CultureInfo.InvariantCulture));

            HistoryBuffer h = state.History;
            c.Charts.Add(state.Chart(n.Name + " throughput", MetricUnitEnum.BytesPerSecond, null,
                ("Received", Charts.Read, h.GetInterfaceSeries(n.Name, MetricIds.NetworkRx)),
                ("Sent", Charts.Write, h.GetInterfaceSeries(n.Name, MetricIds.NetworkTx))));
            c.Charts.Add(state.Chart(n.Name + " packets", MetricUnitEnum.PerSecond, null,
                ("Received", Charts.Read, h.GetInterfaceSeries(n.Name, MetricIds.NetworkRxPackets)),
                ("Sent", Charts.Write, h.GetInterfaceSeries(n.Name, MetricIds.NetworkTxPackets))));
            return c;
        }

        public static DetailsContent Connection(TaskManagerState state, TcpConnectionSnapshot t)
        {
            DetailsContent c = new DetailsContent();
            c.Section("Connection");
            c.Row("State", t.State);
            c.Row("Local endpoint", t.Local + Service(t.Local));
            c.Row("Remote endpoint", t.Remote + Service(t.Remote));
            NetworkInterfaceSnapshot owner = state.Latest?.Network.FirstOrDefault(n => (n.Addresses ?? new List<string>()).Any(a => SameHost(t.Local, a)));
            c.Row("Local interface", owner != null ? owner.Name + " (" + Or(owner.Description) + ")" : "Loopback or not identified");
            c.Row("Direction", Port(t.Local) < Port(t.Remote) && Port(t.Local) < 49152 ? "Inbound (local port is the service)" : "Outbound (remote port is the service)");
            c.Section("Note");
            c.Row("Owning process", "Not available yet: the library does not map TCP connections to processes.");
            return c;
        }

        public static DetailsContent Volume(VolumeSnapshot v)
        {
            DetailsContent c = new DetailsContent();
            c.Section("Volume");
            c.Row("Mount point", v.Name);
            c.Row("Format", Or(v.Format));
            c.Row("Size", MetricFormatter.Bytes(v.TotalBytes));
            c.Row("Used", MetricFormatter.Bytes(v.UsedBytes));
            c.Row("Free", MetricFormatter.Bytes(v.FreeBytes));
            c.Row("Used %", v.TotalBytes > 0 ? MetricFormatter.Format(100.0 * v.UsedBytes / v.TotalBytes, MetricUnitEnum.Percent) : MetricFormatter.NotAvailable);
            c.Row("Free %", v.TotalBytes > 0 ? MetricFormatter.Format(100.0 * v.FreeBytes / v.TotalBytes, MetricUnitEnum.Percent) : MetricFormatter.NotAvailable);
            return c;
        }

        public static DetailsContent Accounting(TaskManagerState state, string group)
        {
            DetailsContent c = new DetailsContent();
            AccountingRow r = state.Accountant.Report(state.GroupBy).FirstOrDefault(x => x.Group == group);
            if (r == null) return c.Row("Group", group + " has no data.");

            c.Section(state.GroupBy == AccountingGroupByEnum.User ? "User" : "Process name");
            c.Row("Group", r.Group);
            c.Row("Since", state.Accountant.SinceUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            c.Row("CPU time", MetricFormatter.Duration(r.CpuSeconds));
            c.Row("Average CPU", MetricFormatter.Format(r.AverageCpuPercent, MetricUnitEnum.Percent));
            c.Row("Peak memory", MetricFormatter.Bytes(r.PeakWorkingSetBytes));
            c.Row("Disk read", MetricFormatter.Bytes(r.DiskReadBytes));
            c.Row("Disk write", MetricFormatter.Bytes(r.DiskWriteBytes));
            c.Row("Processes seen", r.ProcessCount.ToString(CultureInfo.InvariantCulture));
            c.Row("First seen", r.FirstSeenUtc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture));
            c.Row("Last seen", r.LastSeenUtc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture));

            List<ProcessSnapshot> running = (state.Latest?.Processes ?? new List<ProcessSnapshot>())
                .Where(p => (state.GroupBy == AccountingGroupByEnum.User ? p.User : p.Name) == group)
                .OrderByDescending(p => p.CpuPercent ?? 0).ToList();
            c.Section("Running now (" + running.Count.ToString(CultureInfo.InvariantCulture) + ")");
            foreach (ProcessSnapshot p in running.Take(50))
            {
                c.Row(p.Pid.ToString(CultureInfo.InvariantCulture), p.Name + "  CPU " + MetricCatalog.ProcessDisplay(MetricIds.ProcessCpu, p)
                    + "  Mem " + MetricCatalog.ProcessDisplay(MetricIds.ProcessWorkingSet, p) + "  " + p.ExecutablePath);
            }

            return c;
        }

        public static DetailsContent System(TaskManagerState state)
        {
            DetailsContent c = new DetailsContent();
            HostInfo host = state.Host;
            c.Section("Host");
            c.Row("Name", host.HostName);
            c.Row("Operating system", host.OperatingSystem);
            c.Row("Architecture", host.Architecture);
            c.Row("CPU", Or(host.CpuModel));
            c.Row("Logical processors", host.LogicalProcessors.ToString(CultureInfo.InvariantCulture));
            c.Row("Physical memory", MetricFormatter.Format(host.TotalMemoryBytes, MetricUnitEnum.Bytes));
            c.Row("Booted", host.BootTimeUtc.HasValue ? host.BootTimeUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : MetricFormatter.NotAvailable);
            c.Row("Collector elevated", host.Elevated ? "Yes" : "No: some processes may be hidden; admin privileges may be required");

            SystemSnapshot s = state.Latest?.System;
            if (s != null)
            {
                c.Section("System");
                foreach (MetricDefinition m in MetricCatalog.ForScope(MetricScopeEnum.System))
                {
                    double? v = MetricCatalog.SystemValue(m.Id, s);
                    string text = MetricFormatter.Format(v, m.Unit);
                    if (!v.HasValue && state.Unavailable(m.Id) != null) text += " (" + state.Unavailable(m.Id) + ")";
                    c.Row(m.Name, text);
                }

                c.Section("Per core");
                double[] cores = s.PerCoreCpuPercent ?? new double[0];
                for (int i = 0; i < cores.Length; i++) c.Row("Core " + i.ToString(CultureInfo.InvariantCulture), MetricFormatter.Format(cores[i], MetricUnitEnum.Percent));

                HistoryBuffer h = state.History;
                c.Charts.Add(state.Chart("CPU utilization", MetricUnitEnum.Percent, 100, ("CPU", Charts.Cpu, h.GetSystemSeries(MetricIds.SystemCpu))));
                c.Charts.Add(state.Chart("Memory and swap in use", MetricUnitEnum.Bytes, s.MemoryTotalBytes,
                    ("Used", Charts.Memory, h.GetSystemSeries(MetricIds.SystemMemoryUsed)),
                    ("Swap", Charts.Swap, h.GetSystemSeries(MetricIds.SystemSwapUsed))));
            }

            return c;
        }

        #endregion

        #region Private-Methods

        private static void Add(DetailsContent c, TaskManagerState state, ProcessSnapshot p, string id)
        {
            string text = MetricCatalog.ProcessDisplay(id, p);
            if (text == MetricFormatter.NotAvailable) text = NotAvailable(state, id);
            c.Row(MetricCatalog.Get(id).Name, text);
        }

        private static string NotAvailable(TaskManagerState state, string id)
        {
            string reason = state.Unavailable(id);
            if (reason == null && !state.Host.Elevated) reason = "admin privileges may be required";
            return MetricFormatter.NotAvailable + (reason != null ? " (" + reason + ")" : string.Empty);
        }

        private static string Parent(TaskManagerState state, int pid)
        {
            ProcessSnapshot parent = state.Latest?.Processes.FirstOrDefault(x => x.Pid == pid);
            return parent != null ? "  (" + parent.Name + ")" : string.Empty;
        }

        private static string Or(string value)
        {
            return String.IsNullOrWhiteSpace(value) ? MetricFormatter.NotAvailable : value;
        }

        private static int Port(string endpoint)
        {
            int colon = endpoint?.LastIndexOf(':') ?? -1;
            int port;
            return colon >= 0 && Int32.TryParse(endpoint.Substring(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out port) ? port : -1;
        }

        private static string Service(string endpoint)
        {
            string name;
            return _WellKnownPorts.TryGetValue(Port(endpoint), out name) ? "  (" + name + ")" : string.Empty;
        }

        private static bool SameHost(string endpoint, string cidr)
        {
            if (String.IsNullOrEmpty(endpoint) || String.IsNullOrEmpty(cidr)) return false;
            string address = cidr.Split('/')[0].Split('%')[0];
            int colon = endpoint.LastIndexOf(':');
            string host = colon > 0 ? endpoint.Substring(0, colon).Trim('[', ']') : endpoint;
            host = host.Split('%')[0];
            if (host.StartsWith("::ffff:", StringComparison.OrdinalIgnoreCase)) host = host.Substring(7);
            return String.Equals(host, address, StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}
