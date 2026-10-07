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

        /// <summary>
        /// Parent process ID, or null if unavailable.
        /// </summary>
        public int? ParentProcessId
        {
            get
            {
                int? pid = ProcessId;
                if (!pid.HasValue) return null;
                try { return ReadParentProcessId(pid.Value); }
                catch { return null; }
            }
        }

        /// <summary>
        /// Full path of the process executable, or empty if unavailable.
        /// </summary>
        public string ExecutablePath
        {
            get { return Cached(ref _ExecutablePath, ReadExecutablePath); }
        }

        /// <summary>
        /// Command line (arguments joined by spaces), or empty if unavailable.
        /// </summary>
        public string CommandLine
        {
            get { return Cached(ref _CommandLine, ReadCommandLine); }
        }

        /// <summary>
        /// Owning user name, or empty if unavailable.
        /// </summary>
        public string UserName
        {
            get { return Cached(ref _UserName, ReadUserName); }
        }

        /// <summary>
        /// Process start time (UTC), or null if unavailable.
        /// </summary>
        public DateTime? StartTimeUtc
        {
            get
            {
                if (_StartTimeRead) return _StartTimeUtc;
                DateTime? value = Read(p => (DateTime?)p.StartTime.ToUniversalTime());
                if (_Process != null)
                {
                    _StartTimeUtc = value;
                    _StartTimeRead = true;
                }

                return value;
            }
        }

        /// <summary>
        /// Scheduling state.
        /// </summary>
        public ProcessStateEnum State
        {
            get
            {
                int? pid = ProcessId;
                if (!pid.HasValue) return ProcessStateEnum.Unknown;
                try { return ReadState(pid.Value); }
                catch { return ProcessStateEnum.Unknown; }
            }
        }

        /// <summary>
        /// Priority on the cross-platform scale, or null if unavailable.
        /// </summary>
        public ProcessPriorityEnum? Priority
        {
            get { return Read(p => (ProcessPriorityEnum?)ProcessActions.FromPriorityClass(p.PriorityClass)); }
        }

        /// <summary>
        /// Total CPU time consumed since the process started, or null if unavailable.
        /// </summary>
        public TimeSpan? TotalProcessorTime
        {
            get { return Read(p => (TimeSpan?)p.TotalProcessorTime); }
        }

        /// <summary>
        /// Bytes read from storage per second, averaged over the interval since the previous read.
        /// </summary>
        public double? DiskReadBytesPerSecond
        {
            get
            {
                DiskIoRates rates;
                if (_Process == null || !_DiskSampler.TryGet(out rates)) return null;
                return Math.Round(rates.ReadBytesPerSecond, 0);
            }
        }

        /// <summary>
        /// Bytes written to storage per second, averaged over the interval since the previous read.
        /// </summary>
        public double? DiskWriteBytesPerSecond
        {
            get
            {
                DiskIoRates rates;
                if (_Process == null || !_DiskSampler.TryGet(out rates)) return null;
                return Math.Round(rates.WriteBytesPerSecond, 0);
            }
        }

        /// <summary>
        /// Total bytes read since the process started, or null if unavailable.
        /// </summary>
        public long? DiskReadBytesTotal
        {
            get
            {
                DiskIoTotals totals;
                return SafeTryReadDiskIo(out totals) ? (long?)totals.ReadBytes : null;
            }
        }

        /// <summary>
        /// Total bytes written since the process started, or null if unavailable.
        /// </summary>
        public long? DiskWriteBytesTotal
        {
            get
            {
                DiskIoTotals totals;
                return SafeTryReadDiskIo(out totals) ? (long?)totals.WriteBytes : null;
            }
        }

        #endregion

        #region Private-Members

        private Process _Process;
        private readonly DeltaSampler<TimeSpan, double> _CpuSampler;
        private readonly DeltaSampler<DiskIoTotals, DiskIoRates> _DiskSampler;
        private string _ExecutablePath = null;
        private string _CommandLine = null;
        private string _UserName = null;
        private DateTime? _StartTimeUtc = null;
        private bool _StartTimeRead = false;
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
            _DiskSampler = new DeltaSampler<DiskIoTotals, DiskIoRates>(SafeTryReadDiskIo, DiskIoRates.Compute);
            _CpuSampler.Prime();
            _DiskSampler.Prime();
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
            sb.Append("  Parent Process ID             : " + Format(ParentProcessId) + Environment.NewLine);
            sb.Append("  Executable Path               : " + ExecutablePath + Environment.NewLine);
            sb.Append("  Command Line                  : " + CommandLine + Environment.NewLine);
            sb.Append("  User Name                     : " + UserName + Environment.NewLine);
            sb.Append("  Start Time (UTC)              : " + (StartTimeUtc.HasValue ? StartTimeUtc.Value.ToString("o", CultureInfo.InvariantCulture) : "N/A") + Environment.NewLine);
            sb.Append("  State                         : " + State + Environment.NewLine);
            sb.Append("  Priority                      : " + (Priority.HasValue ? Priority.Value.ToString() : "N/A") + Environment.NewLine);
            sb.Append("  CPU Utilization               : " + Format(CpuUtilizationPercent) + "%" + Environment.NewLine);
            sb.Append("  Handle Count                  : " + Format(HandleCount) + Environment.NewLine);
            sb.Append("  Thread Count                  : " + Format(ThreadCount) + Environment.NewLine);
            sb.Append("  Disk Read (Bytes/sec)         : " + Format(DiskReadBytesPerSecond) + Environment.NewLine);
            sb.Append("  Disk Write (Bytes/sec)        : " + Format(DiskWriteBytesPerSecond) + Environment.NewLine);
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
        /// Read the parent process ID. Platform hook; may throw (callers catch).
        /// </summary>
        /// <param name="pid">Process ID.</param>
        /// <returns>Parent ID or null.</returns>
        internal virtual int? ReadParentProcessId(int pid)
        {
            return null;
        }

        /// <summary>
        /// Read the executable path. Platform hook; may throw (callers catch). The default uses the main module.
        /// </summary>
        /// <param name="pid">Process ID.</param>
        /// <returns>Path or empty.</returns>
        internal virtual string ReadExecutablePath(int pid)
        {
            Process p = _Process;
            return p?.MainModule?.FileName ?? string.Empty;
        }

        /// <summary>
        /// Read the command line. Platform hook; may throw (callers catch).
        /// </summary>
        /// <param name="pid">Process ID.</param>
        /// <returns>Command line or empty.</returns>
        internal virtual string ReadCommandLine(int pid)
        {
            return string.Empty;
        }

        /// <summary>
        /// Read the owning user name. Platform hook; may throw (callers catch).
        /// </summary>
        /// <param name="pid">Process ID.</param>
        /// <returns>User name or empty.</returns>
        internal virtual string ReadUserName(int pid)
        {
            return string.Empty;
        }

        /// <summary>
        /// Read the scheduling state. Platform hook; may throw (callers catch).
        /// </summary>
        /// <param name="pid">Process ID.</param>
        /// <returns>State.</returns>
        internal virtual ProcessStateEnum ReadState(int pid)
        {
            return ProcessStateEnum.Unknown;
        }

        /// <summary>
        /// Read cumulative storage bytes read and written. Platform hook; may throw (callers catch).
        /// </summary>
        /// <param name="pid">Process ID.</param>
        /// <param name="readBytes">Bytes read.</param>
        /// <param name="writeBytes">Bytes written.</param>
        /// <returns>True if read.</returns>
        internal virtual bool TryReadDiskIoBytes(int pid, out long readBytes, out long writeBytes)
        {
            readBytes = 0;
            writeBytes = 0;
            return false;
        }

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

        private string Cached(ref string field, Func<int, string> reader)
        {
            if (field != null) return field;
            int? pid = ProcessId;
            if (!pid.HasValue) return string.Empty;

            string value;
            try { value = reader(pid.Value) ?? string.Empty; }
            catch { value = string.Empty; }

            // Cache even an empty result: metadata that is denied once stays denied, and retrying every
            // sampling pass for hundreds of processes is expensive.
            field = value;
            return value;
        }

        private bool SafeTryReadDiskIo(out DiskIoTotals totals)
        {
            totals = default(DiskIoTotals);
            int? pid = ProcessId;
            if (!pid.HasValue) return false;
            try
            {
                long read, write;
                if (!TryReadDiskIoBytes(pid.Value, out read, out write)) return false;
                totals = new DiskIoTotals(0, 0, read, write);
                return true;
            }
            catch
            {
                return false;
            }
        }

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
