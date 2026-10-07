import { api } from '../api.js';
import { bytes, format } from '../format.js';
import { useSeries } from '../hooks/useData.js';
import { unavailableReason } from '../catalog.js';
import Chart from '../components/Chart.jsx';
import { Bar, StatTile } from '../components/Controls.jsx';

export default function Overview({ hostId, latest, history, range, catalog, platform }) {
  const s = latest.system;
  const sys = (metric) => ({ live: (w) => history.systemSeries(metric, w), fetch: (f, t, st) => api.history(hostId, metric, f, t, st) });
  const cores = s.perCoreCpuPercent || [];

  const cpu = useSeries(range, [sys('system.cpu')], latest, [hostId]);
  const perCore = useSeries(range, cores.map((_, i) => sys(`system.cpu.core.${i}`)), latest, [hostId, cores.length]);
  const memory = useSeries(range, [sys('system.memory.used'), sys('system.swap.used')], latest, [hostId]);
  const disk = useSeries(range, [sys('system.disk.read.bytes'), sys('system.disk.write.bytes')], latest, [hostId]);
  const net = useSeries(range, [sys('system.network.rx'), sys('system.network.tx')], latest, [hostId]);
  const load = useSeries(range, [sys('system.load.1'), sys('system.processes')], latest, [hostId]);

  const memPct = s.memoryTotalBytes ? (100 * (s.memoryUsedBytes ?? 0)) / s.memoryTotalBytes : null;
  const loadReason = unavailableReason(catalog, 'system.load.1', platform);

  return (
    <section>
      <div className="tiles">
        <StatTile label="CPU" value={format(s.cpuPercent, 'Percent')} sub={`${cores.length} cores`} />
        <StatTile label="Memory" value={format(memPct, 'Percent')} sub={`${format(s.memoryUsedBytes, 'Bytes')} of ${format(s.memoryTotalBytes, 'Bytes')}`} />
        <StatTile label="Swap" value={format(s.swapUsedBytes, 'Bytes')} sub={`of ${format(s.swapTotalBytes, 'Bytes')}`} reason={unavailableReason(catalog, 'system.swap.used', platform)} />
        <StatTile label="Disk" value={format(s.diskReadBytesPerSecond, 'BytesPerSecond')} sub={`write ${format(s.diskWriteBytesPerSecond, 'BytesPerSecond')}`} />
        <StatTile label="Network" value={format(s.networkReceivedBytesPerSecond, 'BytesPerSecond')} sub={`sent ${format(s.networkSentBytesPerSecond, 'BytesPerSecond')}`} />
        <StatTile label="Load (1/5/15)" value={s.loadAverage1 != null ? `${format(s.loadAverage1, 'Count')} / ${format(s.loadAverage5, 'Count')} / ${format(s.loadAverage15, 'Count')}` : 'n/a'} reason={loadReason} />
        <StatTile label="Processes" value={format(s.processCount, 'Count')} sub={`${format(s.threadCount, 'Count')} threads · ${format(s.handleCount, 'Count')} handles`} />
      </div>

      <div className="charts two">
        <Chart title="CPU utilization" data={cpu} series={[{ label: 'CPU' }]} unit="Percent" max={100} />
        <div className="card cores">
          <h4>Per core</h4>
          <div className="core-grid">
            {cores.map((c, i) => (
              <div key={i} className="core"><span className="muted small">#{i}</span><Bar percent={c} /><span className="num small">{format(c, 'Percent')}</span></div>
            ))}
          </div>
          <div className="muted small">Memory {format(memPct, 'Percent')}</div>
          <Bar percent={memPct} />
        </div>
      </div>
      <Chart title="Per-core CPU over time" data={perCore} series={cores.map((_, i) => ({ label: `#${i}` }))} unit="Percent" max={100} height={200} />
      <div className="charts two">
        <Chart title="Memory and swap in use" data={memory} series={[{ label: 'Memory used' }, { label: 'Swap used' }]} unit="Bytes" max={s.memoryTotalBytes || undefined} />
        <Chart title="Load average (1m) and processes" data={load} series={[{ label: 'Load 1m' }, { label: 'Processes' }]} unit="Count" />
      </div>
      <div className="charts two">
        <Chart title="Disk I/O" data={disk} series={[{ label: 'Read' }, { label: 'Write' }]} unit="BytesPerSecond" />
        <Chart title="Network" data={net} series={[{ label: 'Received' }, { label: 'Sent' }]} unit="BytesPerSecond" />
      </div>
      <p className="muted small">Memory total {bytes(s.memoryTotalBytes || 0)} · available {format(s.memoryAvailableBytes, 'Bytes')}</p>
    </section>
  );
}
