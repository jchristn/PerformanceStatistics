using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// SQLite history store. Series are stored in a compact long format (series id, resolution, timestamp, average,
    /// maximum) at three resolutions: raw samples, 1-minute, and 1-hour aggregates. <see cref="CompactAsync"/> rolls up
    /// completed minutes and hours and applies retention; call it about once a minute. Thread-safe.
    /// </summary>
    public sealed class SqliteHistoryStore : IHistoryStore
    {
        #region Public-Members

        /// <summary>
        /// Retention and recording policy.
        /// </summary>
        public HistoryStoreSettings Settings { get; private set; }

        #endregion

        #region Private-Members

        private const int KindSystem = 0;
        private const int KindInterface = 1;
        private const int KindProcess = 2;
        private const int ResRaw = 1;
        private const int ResMinute = 60;
        private const int ResHour = 3600;

        private static readonly string[] _ProcessMetrics = new string[]
        {
            MetricIds.ProcessCpu, MetricIds.ProcessWorkingSet, MetricIds.ProcessPrivate, MetricIds.ProcessDiskRead,
            MetricIds.ProcessDiskWrite, MetricIds.ProcessHandles, MetricIds.ProcessThreads
        };

        private static readonly string[] _InterfaceMetrics = new string[]
        {
            MetricIds.NetworkRx, MetricIds.NetworkTx, MetricIds.NetworkRxPackets, MetricIds.NetworkTxPackets
        };

        private readonly SemaphoreSlim _Gate = new SemaphoreSlim(1, 1);
        private readonly SqliteConnection _Connection;
        private readonly Dictionary<string, long> _SeriesIds = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Dictionary<string, DateTime> _LastProcessWrite = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="path">Database file path, or ":memory:".</param>
        /// <param name="settings">Settings; null uses defaults.</param>
        public SqliteHistoryStore(string path, HistoryStoreSettings settings = null)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
            Settings = settings ?? new HistoryStoreSettings();
            SqliteConnectionStringBuilder csb = new SqliteConnectionStringBuilder { DataSource = path, Cache = SqliteCacheMode.Private };
            _Connection = new SqliteConnection(csb.ToString());
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task InitializeAsync(CancellationToken token = default)
        {
            await _Gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (_Connection.State != System.Data.ConnectionState.Open) await _Connection.OpenAsync(token).ConfigureAwait(false);
                await ExecuteAsync(@"
                    PRAGMA journal_mode=WAL;
                    PRAGMA synchronous=NORMAL;
                    CREATE TABLE IF NOT EXISTS hosts (host_id TEXT PRIMARY KEY, info TEXT NOT NULL, last_seen INTEGER NOT NULL);
                    CREATE TABLE IF NOT EXISTS series (
                        id INTEGER PRIMARY KEY AUTOINCREMENT, host_id TEXT NOT NULL, kind INTEGER NOT NULL,
                        owner TEXT NOT NULL, metric TEXT NOT NULL, UNIQUE (host_id, kind, owner, metric));
                    CREATE TABLE IF NOT EXISTS samples (
                        series_id INTEGER NOT NULL, res INTEGER NOT NULL, ts INTEGER NOT NULL, avg REAL, max REAL,
                        PRIMARY KEY (series_id, res, ts)) WITHOUT ROWID;
                    CREATE INDEX IF NOT EXISTS idx_samples_res_ts ON samples (res, ts);
                    CREATE TABLE IF NOT EXISTS processes (
                        host_id TEXT NOT NULL, key TEXT NOT NULL, pid INTEGER NOT NULL, ppid INTEGER, name TEXT, path TEXT,
                        cmdline TEXT, user TEXT, start_ts INTEGER, first_seen INTEGER NOT NULL, last_seen INTEGER NOT NULL,
                        PRIMARY KEY (host_id, key));
                    CREATE INDEX IF NOT EXISTS idx_processes_seen ON processes (host_id, last_seen);
                    CREATE TABLE IF NOT EXISTS rollups (res INTEGER PRIMARY KEY, until_ts INTEGER NOT NULL);", null, token).ConfigureAwait(false);
            }
            finally
            {
                _Gate.Release();
            }
        }

        /// <inheritdoc />
        public async Task UpsertHostAsync(HostInfo info, CancellationToken token = default)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            await _Gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                await ExecuteAsync(
                    "INSERT INTO hosts (host_id, info, last_seen) VALUES ($h, $i, $t) ON CONFLICT(host_id) DO UPDATE SET info = excluded.info;",
                    new Dictionary<string, object> { { "$h", info.HostId }, { "$i", MonitoringJson.Serialize(info) }, { "$t", ToMs(DateTime.UtcNow) } },
                    token).ConfigureAwait(false);
            }
            finally
            {
                _Gate.Release();
            }
        }

        /// <inheritdoc />
        public async Task<List<HostRecord>> GetHostsAsync(CancellationToken token = default)
        {
            await _Gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                List<HostRecord> ret = new List<HostRecord>();
                using (SqliteCommand cmd = _Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT info, last_seen FROM hosts ORDER BY host_id;";
                    using (SqliteDataReader r = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await r.ReadAsync(token).ConfigureAwait(false))
                        {
                            HostInfo info = MonitoringJson.Deserialize<HostInfo>(r.GetString(0)) ?? new HostInfo();
                            ret.Add(new HostRecord { Info = info, LastSeenUtc = FromMs(r.GetInt64(1)) });
                        }
                    }
                }

                return ret;
            }
            finally
            {
                _Gate.Release();
            }
        }

        /// <inheritdoc />
        public async Task WriteAsync(HostSnapshot snapshot, CancellationToken token = default)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            long ts = ToMs(snapshot.TimestampUtc);

            await _Gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                using (SqliteTransaction tx = _Connection.BeginTransaction())
                using (SqliteCommand insert = _Connection.CreateCommand())
                {
                    insert.Transaction = tx;
                    insert.CommandText = "INSERT OR REPLACE INTO samples (series_id, res, ts, avg, max) VALUES ($s, 1, $t, $v, $v);";
                    SqliteParameter pSeries = insert.Parameters.Add("$s", SqliteType.Integer);
                    SqliteParameter pTs = insert.Parameters.Add("$t", SqliteType.Integer);
                    SqliteParameter pValue = insert.Parameters.Add("$v", SqliteType.Real);
                    pTs.Value = ts;

                    Func<long, double?, Task> write = async (seriesId, value) =>
                    {
                        if (!value.HasValue || Double.IsNaN(value.Value)) return;
                        pSeries.Value = seriesId;
                        pValue.Value = value.Value;
                        await insert.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    };

                    await ExecuteAsync(
                        "INSERT INTO hosts (host_id, info, last_seen) VALUES ($h, '{}', $t) ON CONFLICT(host_id) DO UPDATE SET last_seen = excluded.last_seen;",
                        new Dictionary<string, object> { { "$h", snapshot.HostId }, { "$t", ts } }, token, tx).ConfigureAwait(false);

                    foreach (MetricDefinition m in MetricCatalog.ForScope(MetricScopeEnum.System))
                    {
                        double? value = MetricCatalog.SystemValue(m.Id, snapshot.System);
                        if (value.HasValue) await write(await SeriesIdAsync(snapshot.HostId, KindSystem, string.Empty, m.Id, tx, token).ConfigureAwait(false), value).ConfigureAwait(false);
                    }

                    double[] cores = snapshot.System.PerCoreCpuPercent ?? new double[0];
                    for (int i = 0; i < cores.Length; i++)
                    {
                        string id = MetricIds.SystemCpuCorePrefix + i.ToString(CultureInfo.InvariantCulture);
                        await write(await SeriesIdAsync(snapshot.HostId, KindSystem, string.Empty, id, tx, token).ConfigureAwait(false), cores[i]).ConfigureAwait(false);
                    }

                    foreach (NetworkInterfaceSnapshot ni in snapshot.Network)
                    {
                        foreach (string metric in _InterfaceMetrics)
                        {
                            double? value = MetricCatalog.InterfaceValue(metric, ni);
                            if (value.HasValue) await write(await SeriesIdAsync(snapshot.HostId, KindInterface, ni.Name, metric, tx, token).ConfigureAwait(false), value).ConfigureAwait(false);
                        }
                    }

                    foreach (ProcessSnapshot p in SelectProcesses(snapshot))
                    {
                        await UpsertProcessAsync(snapshot.HostId, p, ts, tx, token).ConfigureAwait(false);
                        foreach (string metric in _ProcessMetrics)
                        {
                            double? value = MetricCatalog.ProcessValue(metric, p);
                            if (value.HasValue) await write(await SeriesIdAsync(snapshot.HostId, KindProcess, p.Key, metric, tx, token).ConfigureAwait(false), value).ConfigureAwait(false);
                        }
                    }

                    tx.Commit();
                }
            }
            finally
            {
                _Gate.Release();
            }
        }

        /// <inheritdoc />
        public Task<List<SeriesPoint>> QuerySystemAsync(string hostId, string metricId, DateTime fromUtc, DateTime toUtc, int stepSeconds, CancellationToken token = default)
        {
            return QuerySeriesAsync(hostId, KindSystem, string.Empty, metricId, fromUtc, toUtc, stepSeconds, token);
        }

        /// <inheritdoc />
        public Task<List<SeriesPoint>> QueryProcessAsync(string hostId, string processKey, string metricId, DateTime fromUtc, DateTime toUtc, int stepSeconds, CancellationToken token = default)
        {
            return QuerySeriesAsync(hostId, KindProcess, processKey ?? string.Empty, metricId, fromUtc, toUtc, stepSeconds, token);
        }

        /// <inheritdoc />
        public Task<List<SeriesPoint>> QueryInterfaceAsync(string hostId, string interfaceName, string metricId, DateTime fromUtc, DateTime toUtc, int stepSeconds, CancellationToken token = default)
        {
            return QuerySeriesAsync(hostId, KindInterface, interfaceName ?? string.Empty, metricId, fromUtc, toUtc, stepSeconds, token);
        }

        /// <inheritdoc />
        public async Task<List<ProcessRecord>> QueryProcessesAsync(string hostId, DateTime fromUtc, DateTime toUtc, CancellationToken token = default)
        {
            await _Gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                List<ProcessRecord> ret = new List<ProcessRecord>();
                using (SqliteCommand cmd = _Connection.CreateCommand())
                {
                    cmd.CommandText = @"SELECT key, pid, ppid, name, path, cmdline, user, start_ts, first_seen, last_seen FROM processes
                                        WHERE host_id = $h AND last_seen >= $f AND first_seen <= $t ORDER BY last_seen DESC;";
                    cmd.Parameters.AddWithValue("$h", hostId ?? string.Empty);
                    cmd.Parameters.AddWithValue("$f", ToMs(fromUtc));
                    cmd.Parameters.AddWithValue("$t", ToMs(toUtc));
                    using (SqliteDataReader r = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await r.ReadAsync(token).ConfigureAwait(false))
                        {
                            ret.Add(new ProcessRecord
                            {
                                Key = r.GetString(0),
                                Pid = r.GetInt32(1),
                                ParentPid = r.IsDBNull(2) ? (int?)null : r.GetInt32(2),
                                Name = r.IsDBNull(3) ? string.Empty : r.GetString(3),
                                ExecutablePath = r.IsDBNull(4) ? string.Empty : r.GetString(4),
                                CommandLine = r.IsDBNull(5) ? string.Empty : r.GetString(5),
                                User = r.IsDBNull(6) ? string.Empty : r.GetString(6),
                                StartTimeUtc = r.IsDBNull(7) ? (DateTime?)null : FromMs(r.GetInt64(7)),
                                FirstSeenUtc = FromMs(r.GetInt64(8)),
                                LastSeenUtc = FromMs(r.GetInt64(9))
                            });
                        }
                    }
                }

                return ret;
            }
            finally
            {
                _Gate.Release();
            }
        }

        /// <inheritdoc />
        public async Task<List<AccountingRow>> QueryAccountingAsync(string hostId, DateTime fromUtc, DateTime toUtc, AccountingGroupByEnum groupBy, CancellationToken token = default)
        {
            int cores = 1;
            foreach (HostRecord h in await GetHostsAsync(token).ConfigureAwait(false))
            {
                if (String.Equals(h.Info.HostId, hostId, StringComparison.Ordinal)) cores = Math.Max(1, h.Info.LogicalProcessors);
            }

            await _Gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                Dictionary<string, AccountingRow> rows = new Dictionary<string, AccountingRow>(StringComparer.Ordinal);
                Dictionary<string, HashSet<string>> keys = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
                Dictionary<string, long> workingSetByGroupAndMinute = new Dictionary<string, long>(StringComparer.Ordinal);
                double windowSeconds = Math.Max(1, (toUtc - fromUtc).TotalSeconds);

                using (SqliteCommand cmd = _Connection.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT p.name, p.user, s.metric, smp.avg, smp.max, smp.ts, p.key
                        FROM samples smp
                        JOIN series s ON s.id = smp.series_id
                        JOIN processes p ON p.host_id = s.host_id AND p.key = s.owner
                        WHERE s.host_id = $h AND s.kind = 2 AND smp.res = 60 AND smp.ts >= $f AND smp.ts < $t
                          AND s.metric IN ($cpu, $ws, $rd, $wr);";
                    cmd.Parameters.AddWithValue("$h", hostId ?? string.Empty);
                    cmd.Parameters.AddWithValue("$f", ToMs(fromUtc));
                    cmd.Parameters.AddWithValue("$t", ToMs(toUtc));
                    cmd.Parameters.AddWithValue("$cpu", MetricIds.ProcessCpu);
                    cmd.Parameters.AddWithValue("$ws", MetricIds.ProcessWorkingSet);
                    cmd.Parameters.AddWithValue("$rd", MetricIds.ProcessDiskRead);
                    cmd.Parameters.AddWithValue("$wr", MetricIds.ProcessDiskWrite);

                    using (SqliteDataReader r = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await r.ReadAsync(token).ConfigureAwait(false))
                        {
                            string name = r.IsDBNull(0) ? string.Empty : r.GetString(0);
                            string user = r.IsDBNull(1) ? string.Empty : r.GetString(1);
                            string group = groupBy == AccountingGroupByEnum.User ? user : name;
                            if (String.IsNullOrEmpty(group)) group = "(unknown)";
                            string metric = r.GetString(2);
                            double avg = r.IsDBNull(3) ? 0 : r.GetDouble(3);
                            double max = r.IsDBNull(4) ? 0 : r.GetDouble(4);
                            long ts = r.GetInt64(5);

                            AccountingRow row;
                            if (!rows.TryGetValue(group, out row))
                            {
                                row = new AccountingRow { Group = group, FirstSeenUtc = FromMs(ts), LastSeenUtc = FromMs(ts) };
                                rows[group] = row;
                                keys[group] = new HashSet<string>(StringComparer.Ordinal);
                            }

                            keys[group].Add(r.GetString(6));
                            DateTime at = FromMs(ts);
                            if (at < row.FirstSeenUtc) row.FirstSeenUtc = at;
                            if (at > row.LastSeenUtc) row.LastSeenUtc = at;

                            if (String.Equals(metric, MetricIds.ProcessCpu, StringComparison.Ordinal)) row.CpuSeconds += avg / 100.0 * cores * 60.0;
                            else if (String.Equals(metric, MetricIds.ProcessDiskRead, StringComparison.Ordinal)) row.DiskReadBytes += avg * 60.0;
                            else if (String.Equals(metric, MetricIds.ProcessDiskWrite, StringComparison.Ordinal)) row.DiskWriteBytes += avg * 60.0;
                            else
                            {
                                string slot = group + "|" + ts.ToString(CultureInfo.InvariantCulture);
                                long current;
                                workingSetByGroupAndMinute.TryGetValue(slot, out current);
                                workingSetByGroupAndMinute[slot] = current + (long)max;
                            }
                        }
                    }
                }

                foreach (KeyValuePair<string, long> e in workingSetByGroupAndMinute)
                {
                    string group = e.Key.Substring(0, e.Key.LastIndexOf('|'));
                    if (e.Value > rows[group].PeakWorkingSetBytes) rows[group].PeakWorkingSetBytes = e.Value;
                }

                foreach (AccountingRow row in rows.Values)
                {
                    row.ProcessCount = keys[row.Group].Count;
                    row.CpuSeconds = Math.Round(row.CpuSeconds, 2);
                    row.AverageCpuPercent = Math.Round(row.CpuSeconds / (windowSeconds * cores) * 100.0, 2);
                    row.DiskReadBytes = Math.Round(row.DiskReadBytes, 0);
                    row.DiskWriteBytes = Math.Round(row.DiskWriteBytes, 0);
                }

                return rows.Values.OrderByDescending(r => r.CpuSeconds).ThenByDescending(r => r.PeakWorkingSetBytes).ToList();
            }
            finally
            {
                _Gate.Release();
            }
        }

        /// <inheritdoc />
        public async Task<int> CompactAsync(DateTime nowUtc, CancellationToken token = default)
        {
            await _Gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                long now = ToMs(nowUtc);
                await RollUpAsync(ResMinute, ResRaw, 60000L, now, token).ConfigureAwait(false);
                await RollUpAsync(ResHour, ResMinute, 3600000L, now, token).ConfigureAwait(false);

                int deleted = 0;
                deleted += await ExecuteAsync("DELETE FROM samples WHERE res = 1 AND ts < $t;",
                    new Dictionary<string, object> { { "$t", now - (long)Settings.RawRetention.TotalMilliseconds } }, token).ConfigureAwait(false);
                deleted += await DeleteByKindAsync(ResMinute, now - (long)Settings.MinuteRetention.TotalMilliseconds, now - (long)Settings.ProcessMinuteRetention.TotalMilliseconds, token).ConfigureAwait(false);
                deleted += await DeleteByKindAsync(ResHour, now - (long)Settings.HourRetention.TotalMilliseconds, now - (long)Settings.ProcessHourRetention.TotalMilliseconds, token).ConfigureAwait(false);
                deleted += await ExecuteAsync("DELETE FROM processes WHERE last_seen < $t;",
                    new Dictionary<string, object> { { "$t", now - (long)Settings.ProcessHourRetention.TotalMilliseconds } }, token).ConfigureAwait(false);

                // Forget write-throttle state for processes not seen for a while.
                DateTime cutoff = nowUtc - TimeSpan.FromTicks(Settings.BackgroundProcessInterval.Ticks * 5);
                foreach (string key in _LastProcessWrite.Where(e => e.Value < cutoff).Select(e => e.Key).ToList()) _LastProcessWrite.Remove(key);

                return deleted;
            }
            finally
            {
                _Gate.Release();
            }
        }

        /// <summary>
        /// Close the database.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            _Gate.Wait();
            try
            {
                _Connection.Dispose();
            }
            finally
            {
                _Gate.Release();
                _Gate.Dispose();
            }
        }

        #endregion

        #region Private-Methods

        private List<ProcessSnapshot> SelectProcesses(HostSnapshot snapshot)
        {
            int n = Math.Max(0, Settings.TopProcessesPerMetric);
            HashSet<string> top = new HashSet<string>(StringComparer.Ordinal);
            foreach (ProcessSnapshot p in snapshot.Processes.OrderByDescending(p => p.CpuPercent ?? 0).Take(n)) top.Add(p.Key);
            foreach (ProcessSnapshot p in snapshot.Processes.OrderByDescending(p => p.WorkingSetBytes ?? 0).Take(n)) top.Add(p.Key);
            foreach (ProcessSnapshot p in snapshot.Processes.OrderByDescending(p => (p.DiskReadBytesPerSecond ?? 0) + (p.DiskWriteBytesPerSecond ?? 0)).Take(n)) top.Add(p.Key);
            foreach (ProcessSnapshot p in snapshot.Processes.OrderByDescending(p => p.HandleCount ?? 0).Take(n)) top.Add(p.Key);

            List<ProcessSnapshot> ret = new List<ProcessSnapshot>();
            foreach (ProcessSnapshot p in snapshot.Processes)
            {
                DateTime last;
                bool due = !_LastProcessWrite.TryGetValue(p.Key, out last) || snapshot.TimestampUtc - last >= Settings.BackgroundProcessInterval;
                if (top.Contains(p.Key) || due)
                {
                    ret.Add(p);
                    _LastProcessWrite[p.Key] = snapshot.TimestampUtc;
                }
            }

            return ret;
        }

        private async Task UpsertProcessAsync(string hostId, ProcessSnapshot p, long ts, SqliteTransaction tx, CancellationToken token)
        {
            await ExecuteAsync(@"
                INSERT INTO processes (host_id, key, pid, ppid, name, path, cmdline, user, start_ts, first_seen, last_seen)
                VALUES ($h, $k, $pid, $ppid, $name, $path, $cmd, $user, $start, $t, $t)
                ON CONFLICT(host_id, key) DO UPDATE SET last_seen = excluded.last_seen, ppid = excluded.ppid,
                    name = excluded.name, path = CASE WHEN excluded.path <> '' THEN excluded.path ELSE processes.path END,
                    cmdline = CASE WHEN excluded.cmdline <> '' THEN excluded.cmdline ELSE processes.cmdline END,
                    user = CASE WHEN excluded.user <> '' THEN excluded.user ELSE processes.user END;",
                new Dictionary<string, object>
                {
                    { "$h", hostId }, { "$k", p.Key }, { "$pid", p.Pid }, { "$ppid", (object)p.ParentPid ?? DBNull.Value },
                    { "$name", p.Name ?? string.Empty }, { "$path", p.ExecutablePath ?? string.Empty }, { "$cmd", p.CommandLine ?? string.Empty },
                    { "$user", p.User ?? string.Empty }, { "$start", p.StartTimeUtc.HasValue ? (object)ToMs(p.StartTimeUtc.Value) : DBNull.Value }, { "$t", ts }
                }, token, tx).ConfigureAwait(false);
        }

        private async Task<long> SeriesIdAsync(string hostId, int kind, string owner, string metric, SqliteTransaction tx, CancellationToken token)
        {
            string cacheKey = hostId + "\u001f" + kind.ToString(CultureInfo.InvariantCulture) + "\u001f" + owner + "\u001f" + metric;
            long id;
            if (_SeriesIds.TryGetValue(cacheKey, out id)) return id;

            Dictionary<string, object> args = new Dictionary<string, object> { { "$h", hostId }, { "$k", kind }, { "$o", owner }, { "$m", metric } };
            await ExecuteAsync("INSERT OR IGNORE INTO series (host_id, kind, owner, metric) VALUES ($h, $k, $o, $m);", args, token, tx).ConfigureAwait(false);
            using (SqliteCommand cmd = _Connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "SELECT id FROM series WHERE host_id = $h AND kind = $k AND owner = $o AND metric = $m;";
                foreach (KeyValuePair<string, object> a in args) cmd.Parameters.AddWithValue(a.Key, a.Value);
                id = Convert.ToInt64(await cmd.ExecuteScalarAsync(token).ConfigureAwait(false), CultureInfo.InvariantCulture);
            }

            _SeriesIds[cacheKey] = id;
            return id;
        }

        private async Task<List<SeriesPoint>> QuerySeriesAsync(string hostId, int kind, string owner, string metricId, DateTime fromUtc, DateTime toUtc, int stepSeconds, CancellationToken token)
        {
            int step = Math.Max(1, stepSeconds);
            int[] resolutions = step >= ResHour ? new int[] { ResHour, ResMinute, ResRaw }
                : step >= ResMinute ? new int[] { ResMinute, ResRaw }
                : new int[] { ResRaw, ResMinute };

            await _Gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                foreach (int res in resolutions)
                {
                    List<SeriesPoint> points = new List<SeriesPoint>();
                    using (SqliteCommand cmd = _Connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT (smp.ts / $step) * $step AS bucket, AVG(smp.avg), MAX(smp.max)
                            FROM samples smp JOIN series s ON s.id = smp.series_id
                            WHERE s.host_id = $h AND s.kind = $k AND s.owner = $o AND s.metric = $m
                              AND smp.res = $res AND smp.ts >= $f AND smp.ts <= $t
                            GROUP BY bucket ORDER BY bucket;";
                        cmd.Parameters.AddWithValue("$step", (long)step * 1000L);
                        cmd.Parameters.AddWithValue("$h", hostId ?? string.Empty);
                        cmd.Parameters.AddWithValue("$k", kind);
                        cmd.Parameters.AddWithValue("$o", owner);
                        cmd.Parameters.AddWithValue("$m", metricId ?? string.Empty);
                        cmd.Parameters.AddWithValue("$res", res);
                        cmd.Parameters.AddWithValue("$f", ToMs(fromUtc));
                        cmd.Parameters.AddWithValue("$t", ToMs(toUtc));
                        using (SqliteDataReader r = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
                        {
                            while (await r.ReadAsync(token).ConfigureAwait(false))
                            {
                                points.Add(new SeriesPoint(
                                    FromMs(r.GetInt64(0)),
                                    r.IsDBNull(1) ? (double?)null : Math.Round(r.GetDouble(1), 2),
                                    r.IsDBNull(2) ? (double?)null : Math.Round(r.GetDouble(2), 2)));
                            }
                        }
                    }

                    if (points.Count > 0) return points;
                }

                return new List<SeriesPoint>();
            }
            finally
            {
                _Gate.Release();
            }
        }

        private async Task RollUpAsync(int res, int source, long unitMs, long now, CancellationToken token)
        {
            long until = now / unitMs * unitMs;
            long since = 0;
            using (SqliteCommand cmd = _Connection.CreateCommand())
            {
                cmd.CommandText = "SELECT until_ts FROM rollups WHERE res = $r;";
                cmd.Parameters.AddWithValue("$r", res);
                object value = await cmd.ExecuteScalarAsync(token).ConfigureAwait(false);
                if (value != null && value != DBNull.Value) since = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }

            if (since >= until) return;

            await ExecuteAsync(@"
                INSERT OR REPLACE INTO samples (series_id, res, ts, avg, max)
                SELECT series_id, $res, (ts / $unit) * $unit, AVG(avg), MAX(max)
                FROM samples WHERE res = $src AND ts >= $since AND ts < $until
                GROUP BY series_id, ts / $unit;",
                new Dictionary<string, object> { { "$res", res }, { "$unit", unitMs }, { "$src", source }, { "$since", since }, { "$until", until } }, token).ConfigureAwait(false);

            await ExecuteAsync("INSERT INTO rollups (res, until_ts) VALUES ($r, $u) ON CONFLICT(res) DO UPDATE SET until_ts = excluded.until_ts;",
                new Dictionary<string, object> { { "$r", res }, { "$u", until } }, token).ConfigureAwait(false);
        }

        private async Task<int> DeleteByKindAsync(int res, long systemCutoff, long processCutoff, CancellationToken token)
        {
            int deleted = await ExecuteAsync(
                "DELETE FROM samples WHERE res = $r AND ts < $t AND series_id IN (SELECT id FROM series WHERE kind <> 2);",
                new Dictionary<string, object> { { "$r", res }, { "$t", systemCutoff } }, token).ConfigureAwait(false);
            deleted += await ExecuteAsync(
                "DELETE FROM samples WHERE res = $r AND ts < $t AND series_id IN (SELECT id FROM series WHERE kind = 2);",
                new Dictionary<string, object> { { "$r", res }, { "$t", processCutoff } }, token).ConfigureAwait(false);
            return deleted;
        }

        private async Task<int> ExecuteAsync(string sql, Dictionary<string, object> args, CancellationToken token, SqliteTransaction tx = null)
        {
            using (SqliteCommand cmd = _Connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = sql;
                if (args != null)
                {
                    foreach (KeyValuePair<string, object> a in args) cmd.Parameters.AddWithValue(a.Key, a.Value ?? DBNull.Value);
                }

                return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static long ToMs(DateTime utc)
        {
            return new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        }

        private static DateTime FromMs(long ms)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
        }

        #endregion
    }
}
