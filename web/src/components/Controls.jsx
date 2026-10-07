import { useEffect, useRef } from 'react';
import { RANGES } from '../hooks/useData.js';

export function RangePicker({ value, onChange }) {
  return (
    <div className="segmented" role="group" aria-label="Time range">
      {RANGES.map((r) => (
        <button key={r.id} type="button" className={r.id === value.id ? 'active' : ''} aria-pressed={r.id === value.id} onClick={() => onChange(r)}>
          {r.label}
        </button>
      ))}
    </div>
  );
}

export function StatTile({ label, value, sub, reason }) {
  return (
    <div className="tile" title={reason || undefined}>
      <div className="tile-label">{label}</div>
      <div className="tile-value">{value}</div>
      {sub && <div className="tile-sub">{sub}</div>}
    </div>
  );
}

export function Bar({ percent }) {
  const p = percent == null ? null : Math.max(0, Math.min(100, percent));
  const level = p == null ? 'none' : p >= 85 ? 'high' : p >= 60 ? 'mid' : 'low';
  return (
    <div className="bar" aria-label={p == null ? 'n/a' : `${p.toFixed(0)}%`}>
      <div className={`bar-fill ${level}`} style={{ width: `${p ?? 0}%` }} />
    </div>
  );
}

export function ConfirmDialog({ title, message, confirmLabel, danger, onConfirm, onCancel, children }) {
  const ref = useRef(null);
  useEffect(() => {
    const d = ref.current;
    if (d && !d.open) d.showModal();
  }, []);
  return (
    <dialog ref={ref} className="dialog" onCancel={(e) => { e.preventDefault(); onCancel(); }}>
      <h3>{title}</h3>
      <p>{message}</p>
      {children}
      <div className="dialog-actions">
        <button type="button" onClick={onCancel}>Cancel</button>
        <button type="button" className={danger ? 'danger' : 'primary'} onClick={onConfirm} autoFocus>{confirmLabel}</button>
      </div>
    </dialog>
  );
}

export function NotAvailable({ reason }) {
  return <span className="na" title={reason || 'Not available'}>n/a</span>;
}
