import { useEffect, useRef } from 'react';
import uPlot from 'uplot';
import 'uplot/dist/uPlot.min.css';
import { format } from '../format.js';

const COLORS = ['--series-1', '--series-2', '--series-3', '--series-4', '--series-5', '--series-6'];

function cssVar(name) {
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim() || '#888';
}

// A time-series chart. data = { x: [unixSeconds], ys: [[values]] }; series = [{ label }]; unit = MetricUnitEnum name.
export default function Chart({ data, series, unit, height = 180, max, min = 0, title }) {
  const container = useRef(null);
  const plot = useRef(null);

  useEffect(() => {
    const el = container.current;
    if (!el) return undefined;
    const axisColor = cssVar('--text-muted');
    const gridColor = cssVar('--grid');
    const opts = {
      width: el.clientWidth || 400,
      height,
      legend: { show: series.length > 1 },
      cursor: { drag: { x: true, y: false } },
      scales: { x: { time: true }, y: { range: (u, lo, hi) => [min, max ?? Math.max(hi * 1.1, unit === 'Percent' ? 10 : 1)] } },
      axes: [
        { stroke: axisColor, grid: { stroke: gridColor, width: 1 }, ticks: { stroke: gridColor } },
        { stroke: axisColor, grid: { stroke: gridColor, width: 1 }, ticks: { stroke: gridColor }, size: 70, values: (u, vals) => vals.map((v) => format(v, unit)) },
      ],
      series: [
        {},
        ...series.map((s, i) => ({
          label: s.label,
          stroke: cssVar(COLORS[i % COLORS.length]),
          fill: series.length === 1 ? cssVar(COLORS[i % COLORS.length]) + '22' : undefined,
          width: 1.5,
          spanGaps: false,
          // Legend shows a dash when the cursor is not over a point; "n/a" is reserved for unavailable metrics.
          value: (u, v) => (v == null ? '–' : format(v, unit)),
        })),
      ],
    };
    plot.current = new uPlot(opts, [[], ...series.map(() => [])], el);
    const ro = new ResizeObserver(() => plot.current?.setSize({ width: el.clientWidth, height }));
    ro.observe(el);
    return () => { ro.disconnect(); plot.current?.destroy(); plot.current = null; };
    // Rebuild when the shape or theme-relevant options change.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [series.map((s) => s.label).join('|'), unit, height, max, min]);

  useEffect(() => {
    if (plot.current && data) plot.current.setData([data.x, ...data.ys]);
  }, [data]);

  return (
    <figure className="chart">
      {title && <figcaption>{title}</figcaption>}
      <div ref={container} className="chart-body" style={{ minHeight: height }} />
      {data && data.x.length === 0 && <div className="chart-empty">No data in this range yet.</div>}
    </figure>
  );
}
