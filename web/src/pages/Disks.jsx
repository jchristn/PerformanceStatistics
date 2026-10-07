import { api } from '../api.js';
import { format } from '../format.js';
import { unavailableReason } from '../catalog.js';
import { useSeries } from '../hooks/useData.js';
import Chart from '../components/Chart.jsx';
import { Bar } from '../components/Controls.jsx';

export default function Disks({ hostId, latest, history, range, catalog, platform }) {
  const s = latest.system;
  const sys = (metric) => ({ live: (w) => history.systemSeries(metric, w), fetch: (f, t, st) => api.history(hostId, metric, f, t, st) });
  const bytesSeries = useSeries(range, [sys('system.disk.read.bytes'), sys('system.disk.write.bytes')], latest, [hostId]);
  const opsSeries = useSeries(range, [sys('system.disk.read.ops'), sys('system.disk.write.ops')], latest, [hostId]);
  const queueReason = unavailableReason(catalog, 'system.disk.read.queue', platform);

  return (
    <section>
      <table className="simple">
        <thead><tr><th>Volume</th><th>Format</th><th className="num">Size</th><th className="num">Used</th><th className="num">Free</th><th>Used</th></tr></thead>
        <tbody>
          {(latest.volumes || []).map((v) => {
            const pct = v.totalBytes ? (100 * v.usedBytes) / v.totalBytes : null;
            return (
              <tr key={v.name}><td className="mono">{v.name}</td><td>{v.format}</td><td className="num">{format(v.totalBytes, 'Bytes')}</td>
                <td className="num">{format(v.usedBytes, 'Bytes')}</td><td className="num">{format(v.freeBytes, 'Bytes')}</td>
                <td><div className="host-metric"><Bar percent={pct} /><span className="num small">{format(pct, 'Percent')}</span></div></td></tr>
            );
          })}
        </tbody>
      </table>
      <div className="charts two">
        <Chart title="Disk bytes" data={bytesSeries} series={[{ label: 'Read' }, { label: 'Write' }]} unit="BytesPerSecond" />
        <Chart title="Disk operations" data={opsSeries} series={[{ label: 'Read ops' }, { label: 'Write ops' }]} unit="PerSecond" />
      </div>
      <p className="small">
        Queue: read {format(s.diskReadQueue, 'Count')}, write {format(s.diskWriteQueue, 'Count')}
        {queueReason && <span className="muted"> ({queueReason})</span>}
      </p>
    </section>
  );
}
