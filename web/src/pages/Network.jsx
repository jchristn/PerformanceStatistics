import { useState } from 'react';
import { api } from '../api.js';
import { format } from '../format.js';
import { useSeries } from '../hooks/useData.js';
import Chart from '../components/Chart.jsx';

export default function Network({ hostId, latest, history, range }) {
  const interfaces = latest.network || [];
  const busiest = [...interfaces].sort((a, b) => (b.receivedBytesPerSecond + b.sentBytesPerSecond) - (a.receivedBytesPerSecond + a.sentBytesPerSecond))[0];
  const [chosen, setChosen] = useState(null);
  const name = chosen || busiest?.name;
  const [hideIdle, setHideIdle] = useState(true);

  const series = useSeries(range, [
    { live: (w) => history.interfaceSeries(name, 'rx', w), fetch: (f, t, s) => api.interfaceHistory(hostId, name, 'network.rx', f, t, s) },
    { live: (w) => history.interfaceSeries(name, 'tx', w), fetch: (f, t, s) => api.interfaceHistory(hostId, name, 'network.tx', f, t, s) },
  ], latest, [hostId, name]);

  const shown = hideIdle ? interfaces.filter((n) => n.name === name || n.receivedBytesPerSecond > 0 || n.sentBytesPerSecond > 0) : interfaces;
  const tcp = latest.tcpConnections || [];

  return (
    <section>
      <div className="toolbar"><label className="small"><input type="checkbox" checked={hideIdle} onChange={(e) => setHideIdle(e.target.checked)} /> Hide idle interfaces</label></div>
      <table className="simple selectable">
        <thead><tr><th>Interface</th><th>Addresses</th><th className="num">Received</th><th className="num">Sent</th><th className="num">Rx packets</th><th className="num">Tx packets</th><th className="num">Speed</th><th>Type</th><th>MAC</th><th>Description</th></tr></thead>
        <tbody>
          {shown.map((n) => (
            <tr key={n.name} className={n.name === name ? 'selected' : ''} onClick={() => setChosen(n.name)}>
              <td>{n.name}</td><td className="mono">{(n.addresses || []).length ? n.addresses.map((a) => <div key={a}>{a}</div>) : <span className="muted">—</span>}</td><td className="num">{format(n.receivedBytesPerSecond, 'BytesPerSecond')}</td><td className="num">{format(n.sentBytesPerSecond, 'BytesPerSecond')}</td>
              <td className="num">{format(n.receivedPacketsPerSecond, 'PerSecond')}</td><td className="num">{format(n.sentPacketsPerSecond, 'PerSecond')}</td>
              <td className="num">{n.speedBitsPerSecond ? `${format(n.speedBitsPerSecond / 1e6, 'Count')} Mb/s` : 'n/a'}</td><td>{n.interfaceType || ''}</td><td className="mono">{n.macAddress || ''}</td><td className="muted">{n.description}</td>
            </tr>
          ))}
        </tbody>
      </table>
      {name && <Chart title={`${name} throughput`} data={series} series={[{ label: 'Received' }, { label: 'Sent' }]} unit="BytesPerSecond" />}
      <h4>TCP connections ({tcp.length})</h4>
      <table className="simple">
        <thead><tr><th>Local</th><th>Remote</th><th>State</th></tr></thead>
        <tbody>{tcp.slice(0, 500).map((c, i) => (<tr key={i}><td className="mono">{c.local}</td><td className="mono">{c.remote}</td><td>{c.state}</td></tr>))}</tbody>
      </table>
    </section>
  );
}
