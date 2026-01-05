using System;
using System.Diagnostics;
using System.Text;

namespace PerformanceStatistics.Mac
{
    /// <summary>
    /// macOS process counters.
    /// </summary>
    public class MacProcessCounters : IProcessCounters
    {
        private bool _disposed = false;
        private Process _process;
        private TimeSpan? _previousCpuTime;
        private DateTime _lastSampleTime;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="process">The process to monitor.</param>
        public MacProcessCounters(Process process)
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
        /// Process title. Typically empty on macOS.
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
        /// CPU utilization using Process.TotalProcessorTime delta.
        /// Requires two samples; first call returns 0.
        /// </summary>
        public double? CpuUtilizationPercent
        {
            get
            {
                try
                {
                    if (_process == null) return null;

                    var currentCpuTime = _process.TotalProcessorTime;

                    if (_previousCpuTime.HasValue)
                    {
                        var elapsed = DateTime.UtcNow - _lastSampleTime;
                        if (elapsed.TotalMilliseconds > 50)
                        {
                            var cpuDelta = currentCpuTime - _previousCpuTime.Value;
                            var cpuPercent = (cpuDelta.TotalMilliseconds / elapsed.TotalMilliseconds) * 100;

                            // Normalize by processor count
                            cpuPercent = cpuPercent / Environment.ProcessorCount;

                            _previousCpuTime = currentCpuTime;
                            _lastSampleTime = DateTime.UtcNow;
                            return Math.Round(Math.Max(0, cpuPercent), 2);
                        }
                    }

                    _previousCpuTime = currentCpuTime;
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
        /// Handle count. Returns 0 on macOS (not available via Process class).
        /// </summary>
        public int? HandleCount => 0;

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
        /// Non-paged system memory. Not applicable on macOS; returns null.
        /// </summary>
        public long? NonPagedSystemMemory => null;

        /// <summary>
        /// Paged system memory. Not applicable on macOS; returns null.
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
        /// Peak paged memory. Not tracked on macOS; returns null.
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
