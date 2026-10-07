import { useMemo, useRef, useState } from 'react';
import { flexRender, getCoreRowModel, getSortedRowModel, useReactTable } from '@tanstack/react-table';
import { useVirtualizer } from '@tanstack/react-virtual';
import { byId, processValue, unavailableReason } from '../catalog.js';
import { format } from '../format.js';
import { processKey } from '../liveHistory.js';

const NUMERIC = new Set(['Percent', 'Bytes', 'BytesPerSecond', 'PerSecond', 'Count', 'Seconds']);

// Fixed widths keep the header aligned with the absolutely positioned (virtualized) rows; path and command
// line take the remaining space.
const WIDTHS = {
  'process.pid': 72, 'process.ppid': 72, 'process.name': 190, 'process.user': 120, 'process.cpu': 72,
  'process.memory.workingset': 96, 'process.memory.private': 96, 'process.memory.virtual': 96,
  'process.disk.read': 96, 'process.disk.write': 96, 'process.disk.read.total': 96, 'process.disk.write.total': 96,
  'process.handles': 80, 'process.threads': 72, 'process.state': 88, 'process.priority': 100,
  'process.cputime': 96, 'process.start': 170,
};

function cellStyle(id) {
  const w = WIDTHS[id];
  return w ? { width: w, minWidth: w, maxWidth: w } : { flex: 1, minWidth: 160 };
}

// Virtualized, sortable process table. Columns come from the catalog; unavailable values show "n/a"
// with the platform's reason as a tooltip.
export default function ProcessTable({ processes, catalog, platform, columns, selectedKey, onSelect, elevated = true }) {
  const [sorting, setSorting] = useState([{ id: 'process.cpu', desc: true }]);

  const columnDefs = useMemo(() => columns.map((id) => {
    const m = byId(catalog, id) || { id, shortName: id, name: id, unit: 'Text' };
    const numeric = NUMERIC.has(m.unit) || id === 'process.pid' || id === 'process.ppid';
    const reason = unavailableReason(catalog, id, platform);
    return {
      id,
      header: m.shortName || m.name,
      meta: { numeric, description: `${m.name}: ${m.description || ''}` },
      accessorFn: (p) => {
        const v = processValue(p, id);
        return numeric ? (v ?? -1) : (v ?? '');
      },
      sortDescFirst: numeric,
      cell: (info) => {
        const v = processValue(info.row.original, id);
        if (v === null || v === undefined || v === '') return <span className="na" title={reason || 'Not available'}>n/a</span>;
        if (id === 'process.start') return new Date(v).toLocaleString();
        if (m.unit === 'Text' || id === 'process.pid' || id === 'process.ppid') return String(v);
        return format(v, m.unit);
      },
    };
  }), [columns, catalog, platform]);

  const table = useReactTable({
    data: processes,
    columns: columnDefs,
    state: { sorting },
    onSortingChange: setSorting,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getRowId: (p) => processKey(p),
  });

  const rows = table.getRowModel().rows;
  const scroller = useRef(null);
  const virtualizer = useVirtualizer({ count: rows.length, getScrollElement: () => scroller.current, estimateSize: () => 28, overscan: 12 });

  const onKeyDown = (e) => {
    if (e.key !== 'ArrowDown' && e.key !== 'ArrowUp') return;
    e.preventDefault();
    const index = rows.findIndex((r) => r.id === selectedKey);
    const next = Math.max(0, Math.min(rows.length - 1, index + (e.key === 'ArrowDown' ? 1 : -1)));
    if (rows[next]) {
      onSelect(rows[next].original);
      virtualizer.scrollToIndex(next);
    }
  };

  return (
    <div className="ptable" ref={scroller} tabIndex={0} onKeyDown={onKeyDown} role="grid" aria-rowcount={rows.length}>
      <table>
        <thead>
          {table.getHeaderGroups().map((hg) => (
            <tr key={hg.id}>
              {hg.headers.map((h) => (
                <th key={h.id} style={cellStyle(h.column.id)} className={h.column.columnDef.meta.numeric ? 'num' : ''} title={h.column.columnDef.meta.description}
                    onClick={h.column.getToggleSortingHandler()} aria-sort={h.column.getIsSorted() === 'asc' ? 'ascending' : h.column.getIsSorted() === 'desc' ? 'descending' : 'none'}>
                  {flexRender(h.column.columnDef.header, h.getContext())}
                  {h.column.getIsSorted() === 'asc' ? ' ▲' : h.column.getIsSorted() === 'desc' ? ' ▼' : ''}
                </th>
              ))}
            </tr>
          ))}
        </thead>
        <tbody style={{ height: virtualizer.getTotalSize() }}>
          {virtualizer.getVirtualItems().map((vi) => {
            const row = rows[vi.index];
            const p = row.original;
            const dim = p.state === 'Stopped' || p.state === 'Zombie';
            return (
              <tr key={row.id} className={`${row.id === selectedKey ? 'selected' : ''} ${dim ? 'dim' : ''}`}
                  style={{ transform: `translateY(${vi.start}px)` }}
                  onClick={() => onSelect(p)} aria-selected={row.id === selectedKey}>
                {row.getVisibleCells().map((c) => (
                  <td key={c.id} style={cellStyle(c.column.id)} className={c.column.columnDef.meta.numeric ? 'num' : ''}>{flexRender(c.column.columnDef.cell, c.getContext())}</td>
                ))}
              </tr>
            );
          })}
        </tbody>
      </table>
      {rows.length === 0 && <div className="empty">No processes match.{elevated ? '' : ' Some processes may be hidden: admin privileges may be required.'}</div>}
    </div>
  );
}
