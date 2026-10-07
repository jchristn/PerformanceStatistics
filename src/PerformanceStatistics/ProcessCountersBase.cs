using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using PerformanceStatistics.Internal;

namespace PerformanceStatistics
{
    /// <summary>
    /// Shared implementation of <see cref="IProcessCounters"/> over a <see cref="System.Diagnostics.Process"/>.
    /// Platform classes override only the members whose source differs by operating system.
    /// <para>CPU utilization is the process's CPU time over wall time since the previous read (or since
    /// construction), divided by the logical processor count, so 100 means every core is saturated.
    /// This is the same on every platform.</para>
    /// <para>Memory values are cached by <see cref="System.Diagnostics.Process"/> until <see cref="Refresh"/>
    /// is called; <see cref="IPerformanceStatistics.MonitoredProcesses"/> refreshes on every access.</para>
    /// </summary>
    public abstract class ProcessCountersBase : IProcessCounters
    {
        #region Public-Members

        /// <summary>
        /// Process ID.
        /// </summary>
        public int? ProcessId
        {
            get
            {
                Process p = _Process;
                if (p == null) return null;
                try { return p.Id; }
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
                Process p = _Process;
                if (p == null) return string.Empty;
                try { return p.ProcessName ?? string.Empty; }
                catch { return string.Empty; }
            }
        }

        /// <summary>
        /// Main window title. Empty on platforms or processes without one.
        /// </summary>
        public string ProcessTitle
        {
            get
            {
                Process p = _Process;
                if (p == null) return string.Empty;
                try { return p.MainWindowTitle ?? string.Empty; }
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
                Process p = _Process;
                if (p == null) return string.Empty;
                try { return p.MachineName ?? string.Empty; }
                catch { return string.Empty; }
            }
        }

        /// <summary>
        /// CPU utilization percentage (0-100 of total machine capacity), averaged over the interval since
        /// the previous read or since construction. Null if the process has exited or is inaccessible.
        /// </summary>
        public double? CpuUtilizationPercent
        {
            get
            {
                double percent;
                if (_Process == null || !_CpuSampler.TryGet(out percent)) return null;
                return Math.Round(percent, 2);
            }
        }

        /// <summary>
        /// Open handle count (Windows) or open file descriptor count (Linux, macOS). Null if unavailable.
        /// </summary>
        public virtual int? HandleCount
        {
            get { return Read(p => (int?)p.HandleCount); }
        }

        /// <summary>
        /// Thread count.
        /// </summary>
        public int? ThreadCount
        {
            get { return Read(p => (int?)p.Threads.Count); }
        }

        /// <summary>
        /// Non-paged system memory in bytes. Windows only; null elsewhere.
        /// </summary>
        public virtual long? NonPagedSystemMemory
        {
            get { return Read(p => (long?)p.NonpagedSystemMemorySize64); }
        }

        /// <summary>
        /// Paged system memory in bytes. Windows only; null elsewhere.
        /// </summary>
        public virtual long? PagedSystemMemory
        {
            get { return Read(p => (long?)p.PagedSystemMemorySize64); }
        }

        /// <summary>
        /// Private memory in bytes. Windows: private bytes. Linux: private memory as reported by .NET.
        /// macOS: physical footprint (Activity Monitor's "Memory" column), null for other users' processes.
        /// </summary>
        public virtual long? PrivateMemory
        {
            get { return Read(p => (long?)p.PrivateMemorySize64); }
        }

        /// <summary>
        /// Virtual memory in bytes.
        /// </summary>
        public long? VirtualMemory
        {
            get { return Read(p => (long?)p.VirtualMemorySize64); }
        }

        /// <summary>
        /// Working set (resident memory) in bytes.
        /// </summary>
        public long? WorkingSetMemory
        {
            get { return Read(p => (long?)p.WorkingSet64); }
        }

        /// <summary>
        /// Peak paged system memory in bytes. Windows only; null elsewhere.
        /// </summary>
        public virtual long? PeakPagedSystemMemory
        {
            get { return Read(p => (long?)p.PeakPagedMemorySize64); }
        }

        /// <summary>
        /// Peak virtual memory in bytes. Null where the platform does not track it (macOS).
        /// </summary>
        public virtual long? PeakVirtualSystemMemory
        {
            get { return Read(p => (long?)p.PeakVirtualMemorySize64); }
        }

        /// <summary>
        /// Peak working set in bytes. Null where the platform does not track it (macOS).
        /// </summary>
        public virtual long? PeakWorkingSetMemory
        {
            get { return Read(p => (long?)p.PeakWorkingSet64); }
        }

        #endregion

        #region Private-Members

        private Process _Process;
        private readonly DeltaSampler<TimeSpan, double> _CpuSampler;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate. Takes ownership of <paramref name="process"/> and disposes it on <see cref="Dispose()"/>.
        /// </summary>
        /// <param name="process">Process.</param>
        /// <exception cref="ArgumentNullException">process is null.</exception>
        internal ProcessCountersBase(Process process)
        {
            _Process = process ?? throw new ArgumentNullException(nameof(process));
            _CpuSampler = new DeltaSampler<TimeSpan, double>(TryReadProcessorTime, ComputeCpuPercent);
            _CpuSampler.Prime();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Discard cached process information so the next reads return current values.
        /// </summary>
        public void Refresh()
        {
            Process p = _Process;
            if (p == null) return;
            try { p.Refresh(); }
            catch { }
        }

        /// <summary>
        /// Human-readable summary of every counter.
        /// </summary>
        /// <returns>String.</returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("  Process ID                    : " + Format(ProcessId) + Environment.NewLine);
            sb.Append("  Process Name                  : " + ProcessName + Environment.NewLine);
            sb.Append("  Process Title                 : " + ProcessTitle + Environment.NewLine);
            sb.Append("  Machine Name                  : " + MachineName + Environment.NewLine);
            sb.Append("  CPU Utilization               : " + Format(CpuUtilizationPercent) + "%" + Environment.NewLine);
            sb.Append("  Handle Count                  : " + Format(HandleCount) + Environment.NewLine);
            sb.Append("  Thread Count                  : " + Format(ThreadCount) + Environment.NewLine);
            sb.Append("  Memory, Private               : " + Format(PrivateMemory) + " bytes" + Environment.NewLine);
            sb.Append("  Memory, Paged System          : " + Format(PagedSystemMemory) + " bytes" + Environment.NewLine);
            sb.Append("  Memory, Peak Paged System     : " + Format(PeakPagedSystemMemory) + " bytes" + Environment.NewLine);
            sb.Append("  Memory, Non-Paged System      : " + Format(NonPagedSystemMemory) + " bytes" + Environment.NewLine);
            sb.Append("  Memory, Virtual               : " + Format(VirtualMemory) + " bytes" + Environment.NewLine);
            sb.Append("  Memory, Peak Virtual          : " + Format(PeakVirtualSystemMemory) + " bytes" + Environment.NewLine);
            sb.Append("  Memory, Working Set           : " + Format(WorkingSetMemory) + " bytes" + Environment.NewLine);
            sb.Append("  Memory, Peak Working Set      : " + Format(PeakWorkingSetMemory) + " bytes" + Environment.NewLine);
            return sb.ToString();
        }

        /// <summary>
        /// Dispose the underlying process handle.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Internal-and-Protected-Methods

        /// <summary>
        /// True if the process is known to have exited. Inaccessible processes are assumed to be running.
        /// </summary>
        /// <returns>True if exited.</returns>
        internal bool HasExited()
        {
            Process p = _Process;
            if (p == null) return true;
            try { return p.HasExited; }
            catch { return false; }
        }

        /// <summary>
        /// Read a value from the process, returning null if the process is gone or the read fails.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="reader">Reader.</param>
        /// <returns>Value or null.</returns>
        internal T? Read<T>(Func<Process, T?> reader) where T : struct
        {
            Process p = _Process;
            if (p == null) return null;
            try { return reader(p); }
            catch { return null; }
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        /// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;

            if (disposing)
            {
                Process p = _Process;
                _Process = null;
                p?.Dispose();
            }

            _Disposed = true;
        }

        #endregion

        #region Private-Methods

        private bool TryReadProcessorTime(out TimeSpan time)
        {
            Process p = _Process;
            time = TimeSpan.Zero;
            if (p == null) return false;
            try
            {
                time = p.TotalProcessorTime;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static double ComputeCpuPercent(TimeSpan previous, TimeSpan current, TimeSpan elapsed)
        {
            if (elapsed.TotalMilliseconds <= 0) return 0;
            double cpuMs = Math.Max(0, (current - previous).TotalMilliseconds);
            double percent = cpuMs / elapsed.TotalMilliseconds / Environment.ProcessorCount * 100.0;
            return Math.Max(0, Math.Min(100, percent));
        }

        private static string Format<T>(T? value) where T : struct, IFormattable
        {
            return value.HasValue ? value.Value.ToString(null, CultureInfo.InvariantCulture) : "N/A";
        }

        #endregion
    }
}
