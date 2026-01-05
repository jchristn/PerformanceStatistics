using System;
using System.Diagnostics;
using System.Text;

namespace PerformanceStatistics.Linux
{
    /// <summary>
    /// Linux process counters.
    /// </summary>
    public class LinuxProcessCounters : IProcessCounters
    {
        private bool _disposed = false;
        private Process _process;
        private (long utime, long stime)? _previousCpuSample;
        private DateTime _lastSampleTime;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="process">The process to monitor.</param>
        public LinuxProcessCounters(Process process)
        {
            _process = process ?? throw new ArgumentNullException(nameof(process));
        }

        /// <summary>
        /// Process ID.
        /// </summary>
        public int? ProcessId
        {
            get
            {
                try { return _process?.Id; }
                catch { return null; }
            }
        }

        /// <summary>
        /// Process name.
        /// </summary>
        public string ProcessName
        {
            get
            {
                try { return _process?.ProcessName ?? string.Empty; }
                catch { return string.Empty; }
            }
        }

        /// <summary>
        /// Process title. Typically empty on Linux (no window concept).
        /// </summary>
        public string ProcessTitle
        {
            get
            {
                try { return _process?.MainWindowTitle ?? string.Empty; }
                catch { return string.Empty; }
            }
        }

        /// <summary>
        /// Machine name.
        /// </summary>
        public string MachineName
        {
            get
            {
                try { return _process?.MachineName ?? string.Empty; }
                catch { return string.Empty; }
            }
        }

        /// <summary>
        /// CPU utilization calculated from /proc/[pid]/stat.
        /// Requires two samples; first call returns 0.
        /// </summary>
        public double? CpuUtilizationPercent
        {
            get
            {
                try
                {
                    int? pid = ProcessId;
                    if (!pid.HasValue) return null;

                    var current = ProcFileParser.GetProcessCpuTimes(pid.Value);
                    if (!current.HasValue) return null;

                    if (_previousCpuSample.HasValue)
                    {
                        var elapsed = DateTime.UtcNow - _lastSampleTime;
                        if (elapsed.TotalMilliseconds > 50)
                        {
                            var ticksPerSecond = ProcFileParser.GetClockTicksPerSecond();
                            var elapsedSeconds = elapsed.TotalSeconds;
                            var cpuTicks = (current.Value.utime + current.Value.stime) -
                                          (_previousCpuSample.Value.utime + _previousCpuSample.Value.stime);
                            var cpuSeconds = cpuTicks / (double)ticksPerSecond;
                            var cpuPercent = (cpuSeconds / elapsedSeconds) * 100;

                            // Normalize by processor count for per-process percentage
                            cpuPercent = cpuPercent / Environment.ProcessorCount;

                            _previousCpuSample = current;
                            _lastSampleTime = DateTime.UtcNow;
                            return Math.Round(Math.Max(0, cpuPercent), 2);
                        }
                    }

                    _previousCpuSample = current;
                    _lastSampleTime = DateTime.UtcNow;
                    return 0;
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Handle count via /proc/[pid]/fd directory count.
        /// Returns 0 if permission denied or process exited.
        /// </summary>
        public int? HandleCount
        {
            get
            {
                try
                {
                    int? pid = ProcessId;
                    if (!pid.HasValue) return 0;

                    var count = ProcFileParser.GetProcessFdCount(pid.Value);
                    return count ?? 0;
                }
                catch
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// Thread count.
        /// </summary>
        public int? ThreadCount
        {
            get
            {
                try { return _process?.Threads?.Count ?? 0; }
                catch { return 0; }
            }
        }

        /// <summary>
        /// Non-paged system memory. Not applicable on Linux; returns null.
        /// </summary>
        public long? NonPagedSystemMemory => null;

        /// <summary>
        /// Paged system memory. Not applicable on Linux; returns null.
        /// </summary>
        public long? PagedSystemMemory => null;

        /// <summary>
        /// Private memory in bytes.
        /// </summary>
        public long? PrivateMemory
        {
            get
            {
                try { return _process?.PrivateMemorySize64; }
                catch { return null; }
            }
        }

        /// <summary>
        /// Virtual memory in bytes.
        /// </summary>
        public long? VirtualMemory
        {
            get
            {
                try { return _process?.VirtualMemorySize64; }
                catch { return null; }
            }
        }

        /// <summary>
        /// Working set memory in bytes.
        /// </summary>
        public long? WorkingSetMemory
        {
            get
            {
                try { return _process?.WorkingSet64; }
                catch { return null; }
            }
        }

        /// <summary>
        /// Peak paged memory. Not tracked on Linux; returns null.
        /// </summary>
        public long? PeakPagedSystemMemory => null;

        /// <summary>
        /// Peak virtual memory in bytes.
        /// </summary>
        public long? PeakVirtualSystemMemory
        {
            get
            {
                try { return _process?.PeakVirtualMemorySize64; }
                catch { return null; }
            }
        }

        /// <summary>
        /// Peak working set memory in bytes.
        /// </summary>
        public long? PeakWorkingSetMemory
        {
            get
            {
                try { return _process?.PeakWorkingSet64; }
                catch { return null; }
            }
        }

        /// <summary>
        /// Produce a human-readable object.
        /// </summary>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("  Process ID                    : " + ProcessId + Environment.NewLine);
            sb.Append("  Process Name                  : " + ProcessName + Environment.NewLine);
            sb.Append("  Process Title                 : " + ProcessTitle + Environment.NewLine);
            sb.Append("  Machine Name                  : " + MachineName + Environment.NewLine);
            sb.Append("  CPU Utilization               : " + (CpuUtilizationPercent?.ToString() ?? "N/A") + "%" + Environment.NewLine);
            sb.Append("  Handle Count                  : " + (HandleCount?.ToString() ?? "N/A") + Environment.NewLine);
            sb.Append("  Thread Count                  : " + ThreadCount + Environment.NewLine);
            sb.Append("  Memory, Private               : " + (PrivateMemory?.ToString() ?? "N/A") + " bytes" + Environment.NewLine);
            sb.Append("  Memory, Paged System          : " + (PagedSystemMemory?.ToString() ?? "N/A") + " bytes" + Environment.NewLine);
            sb.Append("  Memory, Peak Paged System     : " + (PeakPagedSystemMemory?.ToString() ?? "N/A") + " bytes" + Environment.NewLine);
            sb.Append("  Memory, Non-Paged System      : " + (NonPagedSystemMemory?.ToString() ?? "N/A") + " bytes" + Environment.NewLine);
            sb.Append("  Memory, Virtual               : " + (VirtualMemory?.ToString() ?? "N/A") + " bytes" + Environment.NewLine);
            sb.Append("  Memory, Peak Virtual          : " + (PeakVirtualSystemMemory?.ToString() ?? "N/A") + " bytes" + Environment.NewLine);
            sb.Append("  Memory, Working Set           : " + (WorkingSetMemory?.ToString() ?? "N/A") + " bytes" + Environment.NewLine);
            sb.Append("  Memory, Peak Working Set      : " + (PeakWorkingSetMemory?.ToString() ?? "N/A") + " bytes" + Environment.NewLine);
            return sb.ToString();
        }

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        /// <param name="disposing">True if disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _process?.Dispose();
                _process = null;
            }

            _disposed = true;
        }
    }
}
