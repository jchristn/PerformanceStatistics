import { api } from '../api.js';
import { usePolling } from '../hooks/useData.js';

export default function Audit({ hostId }) {
  const { data, error } = usePolling(() => api.audit(hostId), [hostId], 10000);
  return (
    <section>
      {error && <p className="error">{error.message}</p>}
      {data && data.length === 0 && <p className="muted">No process actions have been requested on this host.</p>}
      {data && data.length > 0 && (
        <table className="simple">
          <thead><tr><th>Time</th><th>Action</th><th className="num">PID</th><th>By</th><th>Result</th><th>Detail</th></tr></thead>
          <tbody>
            {data.map((e, i) => (
              <tr key={i}><td>{new Date(e.timestampUtc).toLocaleString()}</td><td>{e.action}</td><td className="num">{e.pid}</td><td>{e.actor}</td>
                <td className={e.result === 'Success' ? 'ok' : e.result === 'Requested' ? '' : 'error'}>{e.result}</td><td className="muted">{e.message}</td></tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
