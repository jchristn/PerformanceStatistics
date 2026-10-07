import { useEffect, useState } from 'react';
import { Link, NavLink, Route, Routes, useParams } from 'react-router-dom';
import { api, getSession } from '../api.js';
import { bytes, duration, format } from '../format.js';
import { RANGES, useCatalog, useHostStream, usePolling } from '../hooks/useData.js';
import { ElevationNotice, RangePicker } from '../components/Controls.jsx';
import Overview from './Overview.jsx';
import Processes from './Processes.jsx';
import Network from './Network.jsx';
import Disks from './Disks.jsx';
import Accounting from './Accounting.jsx';
import Audit from './Audit.jsx';

const TABS = [
  ['', 'Overview'], ['processes', 'Processes'], ['network', 'Network'], ['disks', 'Disks'], ['accounting', 'Accounting'], ['audit', 'Audit'],
];

export default function Host() {
  const { hostId } = useParams();
  const catalog = useCatalog();
  const { latest, status, history } = useHostStream(hostId);
  const { data: host } = usePolling(() => api.host(hostId), [hostId], 10000);
  const [range, setRange] = useState(RANGES[0]);
  const [toast, setToast] = useState(null);
  const session = getSession();

  useEffect(() => {
    if (!toast) return undefined;
    const t = setTimeout(() => setToast(null), 6000);
    return () => clearTimeout(t);
  }, [toast]);

  const info = host?.info;
  const ctx = {
    hostId, catalog, latest, history, range, platform: info?.platform, elevated: info ? info.elevated : true,
    allowedActions: host?.allowedActions || [], isAdmin: !!session?.isAdmin,
    notify: (text, kind) => setToast({ text, kind }),
  };

  return (
    <main className="page">
      <div className="host-bar">
        <div>
          <Link to="/" className="muted small">← Hosts</Link>
          <h2>{info?.hostName || hostId} <span className={`badge ${status === 'live' ? 'ok' : 'off'}`}>{status}</span></h2>
          {info && (
            <div className="muted small">
              {info.operatingSystem} · {info.architecture} · {info.cpuModel || 'CPU'} · {info.logicalProcessors} cores · {info.totalMemoryBytes ? bytes(info.totalMemoryBytes) : 'n/a'} RAM
              {latest?.system?.uptimeSeconds != null && <> · up {duration(latest.system.uptimeSeconds)}</>}
              {!info.elevated && <> · <ElevationNotice platform={info.platform} compact /></>}
            </div>
          )}
        </div>
        <RangePicker value={range} onChange={setRange} />
      </div>

      <nav className="tabs" aria-label="Host views">
        {TABS.map(([path, label]) => (
          <NavLink key={label} end to={path} className={({ isActive }) => (isActive ? 'active' : '')}>{label}</NavLink>
        ))}
      </nav>

      {info && !info.elevated && <ElevationNotice platform={info.platform} />}
      {!latest && <p className="muted">Waiting for data from this host…</p>}
      {latest && catalog && (
        <Routes>
          <Route index element={<Overview {...ctx} />} />
          <Route path="processes" element={<Processes {...ctx} />} />
          <Route path="network" element={<Network {...ctx} />} />
          <Route path="disks" element={<Disks {...ctx} />} />
          <Route path="accounting" element={<Accounting {...ctx} />} />
          <Route path="audit" element={<Audit {...ctx} />} />
        </Routes>
      )}

      {toast && <div className={`toast ${toast.kind}`} role="status">{toast.text}</div>}
      <footer className="muted small foot">CPU % is of the whole machine on every platform. {format(latest?.system?.processCount, 'Count')} processes.</footer>
    </main>
  );
}
