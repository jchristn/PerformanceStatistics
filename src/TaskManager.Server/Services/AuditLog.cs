using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using PerformanceStatistics.Monitoring;

namespace TaskManager.Server
{
    /// <summary>
    /// Durable audit trail of process action requests and outcomes, in the server's SQLite database.
    /// </summary>
    public sealed class AuditLog : IDisposable
    {
        #region Private-Members

        private readonly SemaphoreSlim _Gate = new SemaphoreSlim(1, 1);
        private readonly SqliteConnection _Connection;

        #endregion

        #region Constructors-and-Factories

        public AuditLog(string path)
        {
            _Connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
        }

        #endregion

        #region Public-Methods

        public async Task InitializeAsync(CancellationToken token = default)
        {
            await _Connection.OpenAsync(token).ConfigureAwait(false);
            using (SqliteCommand cmd = _Connection.CreateCommand())
            {
                cmd.CommandText = @"PRAGMA journal_mode=WAL;
                    CREATE TABLE IF NOT EXISTS audit (
                        id INTEGER PRIMARY KEY AUTOINCREMENT, ts INTEGER NOT NULL, request_id TEXT NOT NULL, host_id TEXT NOT NULL,
                        pid INTEGER NOT NULL, action TEXT NOT NULL, actor TEXT NOT NULL, result TEXT NOT NULL, message TEXT);
                    CREATE INDEX IF NOT EXISTS idx_audit_host ON audit (host_id, ts);";
                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        public async Task WriteAsync(string requestId, string hostId, int pid, string action, string actor, string result, string message, CancellationToken token = default)
        {
            await _Gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                using (SqliteCommand cmd = _Connection.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO audit (ts, request_id, host_id, pid, action, actor, result, message) VALUES ($ts, $r, $h, $p, $a, $u, $res, $m);";
                    cmd.Parameters.AddWithValue("$ts", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                    cmd.Parameters.AddWithValue("$r", requestId ?? string.Empty);
                    cmd.Parameters.AddWithValue("$h", hostId ?? string.Empty);
                    cmd.Parameters.AddWithValue("$p", pid);
                    cmd.Parameters.AddWithValue("$a", action ?? string.Empty);
                    cmd.Parameters.AddWithValue("$u", actor ?? string.Empty);
                    cmd.Parameters.AddWithValue("$res", result ?? string.Empty);
                    cmd.Parameters.AddWithValue("$m", (object)message ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
            finally
            {
                _Gate.Release();
            }
        }

        public async Task<List<AuditEntry>> ReadAsync(string hostId, int limit, CancellationToken token = default)
        {
            await _Gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                List<AuditEntry> ret = new List<AuditEntry>();
                using (SqliteCommand cmd = _Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT ts, request_id, pid, action, actor, result, message FROM audit WHERE host_id = $h ORDER BY id DESC LIMIT $l;";
                    cmd.Parameters.AddWithValue("$h", hostId ?? string.Empty);
                    cmd.Parameters.AddWithValue("$l", Math.Max(1, Math.Min(1000, limit)));
                    using (SqliteDataReader r = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await r.ReadAsync(token).ConfigureAwait(false))
                        {
                            ret.Add(new AuditEntry
                            {
                                TimestampUtc = DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(0)).UtcDateTime,
                                RequestId = r.GetString(1),
                                HostId = hostId,
                                Pid = r.GetInt32(2),
                                Action = r.GetString(3),
                                Actor = r.GetString(4),
                                Result = r.GetString(5),
                                Message = r.IsDBNull(6) ? null : r.GetString(6)
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

        public void Dispose()
        {
            _Connection.Dispose();
            _Gate.Dispose();
        }

        #endregion

        #region Public-Classes

        public sealed class AuditEntry
        {
            public DateTime TimestampUtc { get; set; }

            public string RequestId { get; set; }

            public string HostId { get; set; }

            public int Pid { get; set; }

            public string Action { get; set; }

            public string Actor { get; set; }

            public string Result { get; set; }

            public string Message { get; set; }
        }

        #endregion
    }
}
