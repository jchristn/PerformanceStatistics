using System;
using System.Collections.Generic;

namespace PerformanceStatistics.Internal
{
    /// <summary>
    /// Cumulative system CPU time counters, in arbitrary platform units (jiffies, Mach ticks, or 100ns intervals).
    /// Only ratios between deltas are used, so the unit does not matter.
    /// </summary>
    internal struct CpuTimes
    {
        /// <summary>
        /// Total CPU time across all states, including idle.
        /// </summary>
        public ulong Total;

        /// <summary>
        /// Idle CPU time (including I/O wait where the platform reports it).
        /// </summary>
        public ulong Idle;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="total">Total time.</param>
        /// <param name="idle">Idle time.</param>
        public CpuTimes(ulong total, ulong idle)
        {
            Total = total;
            Idle = idle;
        }

        /// <summary>
        /// Busy percentage (0-100) between two samples, or 0 when no time elapsed.
        /// </summary>
        /// <param name="previous">Earlier sample.</param>
        /// <param name="current">Later sample.</param>
        /// <returns>Percentage.</returns>
        public static double BusyPercent(CpuTimes previous, CpuTimes current)
        {
            if (current.Total <= previous.Total) return 0;
            double total = current.Total - previous.Total;
            double idle = current.Idle >= previous.Idle ? current.Idle - previous.Idle : 0;
            double percent = 100.0 * (total - idle) / total;
            return Math.Max(0, Math.Min(100, percent));
        }
    }

    /// <summary>
    /// Cumulative completed disk operations since boot (or since counter creation).
    /// </summary>
    internal struct DiskIoTotals
    {
        /// <summary>
        /// Completed read operations.
        /// </summary>
        public long Reads;

        /// <summary>
        /// Completed write operations.
        /// </summary>
        public long Writes;

        /// <summary>
        /// Bytes read.
        /// </summary>
        public long ReadBytes;

        /// <summary>
        /// Bytes written.
        /// </summary>
        public long WriteBytes;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="reads">Completed reads.</param>
        /// <param name="writes">Completed writes.</param>
        /// <param name="readBytes">Bytes read.</param>
        /// <param name="writeBytes">Bytes written.</param>
        public DiskIoTotals(long reads, long writes, long readBytes = 0, long writeBytes = 0)
        {
            Reads = reads;
            Writes = writes;
            ReadBytes = readBytes;
            WriteBytes = writeBytes;
        }
    }

    /// <summary>
    /// Disk operations per second over a sampling interval.
    /// </summary>
    internal struct DiskIoRates
    {
        /// <summary>
        /// Read operations per second.
        /// </summary>
        public double ReadsPerSecond;

        /// <summary>
        /// Write operations per second.
        /// </summary>
        public double WritesPerSecond;

        /// <summary>
        /// Bytes read per second.
        /// </summary>
        public double ReadBytesPerSecond;

        /// <summary>
        /// Bytes written per second.
        /// </summary>
        public double WriteBytesPerSecond;

        /// <summary>
        /// Compute rates between two cumulative samples. Counter resets (current lower than previous) yield zero.
        /// </summary>
        /// <param name="previous">Earlier sample.</param>
        /// <param name="current">Later sample.</param>
        /// <param name="elapsed">Time between samples.</param>
        /// <returns>Rates.</returns>
        public static DiskIoRates Compute(DiskIoTotals previous, DiskIoTotals current, TimeSpan elapsed)
        {
            DiskIoRates ret = new DiskIoRates();
            if (elapsed.TotalSeconds <= 0) return ret;
            ret.ReadsPerSecond = Math.Max(0, current.Reads - previous.Reads) / elapsed.TotalSeconds;
            ret.WritesPerSecond = Math.Max(0, current.Writes - previous.Writes) / elapsed.TotalSeconds;
            ret.ReadBytesPerSecond = Math.Max(0, current.ReadBytes - previous.ReadBytes) / elapsed.TotalSeconds;
            ret.WriteBytesPerSecond = Math.Max(0, current.WriteBytes - previous.WriteBytes) / elapsed.TotalSeconds;
            return ret;
        }
    }

    /// <summary>
    /// Cumulative counters for one network interface.
    /// </summary>
    internal struct NetworkTotals
    {
        /// <summary>
        /// Interface description.
        /// </summary>
        public string Description;

        /// <summary>
        /// Link speed in bits per second, or null.
        /// </summary>
        public long? Speed;

        /// <summary>
        /// Bytes received.
        /// </summary>
        public long ReceivedBytes;

        /// <summary>
        /// Bytes sent.
        /// </summary>
        public long SentBytes;

        /// <summary>
        /// Packets received, or null if not reported.
        /// </summary>
        public long? ReceivedPackets;

        /// <summary>
        /// Packets sent, or null if not reported.
        /// </summary>
        public long? SentPackets;

        /// <summary>
        /// Unicast addresses in CIDR form (e.g. "192.168.1.10/24", "fe80::1%en0/64").
        /// </summary>
        public List<string> Addresses;

        /// <summary>
        /// MAC address as colon-separated hex, or empty.
        /// </summary>
        public string MacAddress;

        /// <summary>
        /// Interface type, e.g. "Ethernet" or "Wireless80211".
        /// </summary>
        public string InterfaceType;
    }
}
