'use client';

// Lightweight inline-SVG charts — no external charting dependency.

/** Small single-series sparkline for stat cards. */
export function MiniSpark({ points, color, height = 40, width = 120 }: { points: number[]; color: string; height?: number; width?: number }) {
  const max = Math.max(1, ...points);
  const min = Math.min(...points);
  const range = max - min || 1;
  const step = width / Math.max(1, points.length - 1);
  const d = points.map((v, i) => `${i === 0 ? 'M' : 'L'}${(i * step).toFixed(1)},${(height - ((v - min) / range) * (height - 6) - 3).toFixed(1)}`).join(' ');
  const area = `${d} L${width},${height} L0,${height} Z`;
  const id = `g-${color.replace(/[^a-z0-9]/gi, '')}`;
  return (
    <svg viewBox={`0 0 ${width} ${height}`} width={width} height={height} aria-hidden="true">
      <defs>
        <linearGradient id={id} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={color} stopOpacity="0.18" />
          <stop offset="100%" stopColor={color} stopOpacity="0" />
        </linearGradient>
      </defs>
      <path d={area} fill={`url(#${id})`} />
      <path d={d} fill="none" stroke={color} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

/** Dual-series line chart with area fill, y-grid and x labels. */
export function TrendChart({
  labels, created, resolved, height = 240,
}: { labels: string[]; created: number[]; resolved: number[]; height?: number }) {
  const width = 640;
  const padL = 30, padB = 22, padT = 8, padR = 8;
  const max = Math.max(10, ...created, ...resolved);
  const yTicks = 4;
  const n = Math.max(created.length, resolved.length, 2);
  const x = (i: number) => padL + (i / (n - 1)) * (width - padL - padR);
  const y = (v: number) => padT + (1 - v / max) * (height - padT - padB);
  const line = (s: number[]) => s.map((v, i) => `${i === 0 ? 'M' : 'L'}${x(i).toFixed(1)},${y(v).toFixed(1)}`).join(' ');
  const areaOf = (s: number[]) => `${line(s)} L${x(n - 1)},${height - padB} L${x(0)},${height - padB} Z`;

  return (
    <svg viewBox={`0 0 ${width} ${height}`} className="w-full" role="img" aria-label="Created vs resolved trend">
      <defs>
        <linearGradient id="cr" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="#3b82f6" stopOpacity="0.16" /><stop offset="100%" stopColor="#3b82f6" stopOpacity="0" />
        </linearGradient>
      </defs>
      {Array.from({ length: yTicks + 1 }, (_, i) => {
        const v = (max / yTicks) * i;
        return (
          <g key={i}>
            <line x1={padL} x2={width - padR} y1={y(v)} y2={y(v)} stroke="var(--border)" strokeWidth="1" />
            <text x={padL - 6} y={y(v) + 3} textAnchor="end" fontSize="9" fill="var(--faint)">{Math.round(v)}</text>
          </g>
        );
      })}
      <path d={areaOf(created)} fill="url(#cr)" />
      <path d={line(created)} fill="none" stroke="#3b82f6" strokeWidth="2.5" strokeLinejoin="round" />
      <path d={line(resolved)} fill="none" stroke="#22c55e" strokeWidth="2.5" strokeLinejoin="round" />
      {created.map((v, i) => <circle key={`c${i}`} cx={x(i)} cy={y(v)} r="3" fill="#3b82f6" />)}
      {resolved.map((v, i) => <circle key={`r${i}`} cx={x(i)} cy={y(v)} r="3" fill="#22c55e" />)}
      {labels.map((l, i) => <text key={l} x={x(i)} y={height - 6} textAnchor="middle" fontSize="9" fill="var(--faint)">{l}</text>)}
    </svg>
  );
}

/** Vertical bar chart with a y-axis. */
export function BarChart({ values, labels, color = '#3b82f6', height = 190, unit = '' }: { values: number[]; labels: string[]; color?: string; height?: number; unit?: string }) {
  const width = 440;
  const padL = 30, padB = 20, padT = 6, padR = 4;
  const max = Math.max(1, ...values);
  const ticks = 5;
  const gap = (width - padL - padR) / values.length;
  const bw = gap * 0.5;
  const y = (v: number) => padT + (1 - v / max) * (height - padT - padB);
  return (
    <svg viewBox={`0 0 ${width} ${height}`} className="w-full" role="img" aria-label="Bar chart">
      {Array.from({ length: ticks + 1 }, (_, i) => {
        const v = (max / ticks) * i;
        return (
          <g key={i}>
            <line x1={padL} x2={width - padR} y1={y(v)} y2={y(v)} stroke="var(--border)" strokeWidth="1" />
            <text x={padL - 5} y={y(v) + 3} textAnchor="end" fontSize="9" fill="var(--faint)">{Math.round(v)}{unit}</text>
          </g>
        );
      })}
      {values.map((v, i) => (
        <rect key={i} x={padL + i * gap + (gap - bw) / 2} y={y(v)} width={bw} height={Math.max(0, height - padB - y(v))} rx="2" fill={color} />
      ))}
      {labels.map((l, i) => <text key={l} x={padL + i * gap + gap / 2} y={height - 6} textAnchor="middle" fontSize="9" fill="var(--faint)">{l}</text>)}
    </svg>
  );
}

/** Single-series line chart with an optional fixed y-range (e.g. 80–100%). */
export function LineChart({ labels, values, color = '#22c55e', height = 190, yMin, yMax, unit = '' }: { labels: string[]; values: number[]; color?: string; height?: number; yMin?: number; yMax?: number; unit?: string }) {
  const width = 440;
  const padL = 34, padB = 20, padT = 8, padR = 6;
  const lo = yMin ?? Math.min(...values);
  const hi = yMax ?? Math.max(...values, 1);
  const range = hi - lo || 1;
  const n = Math.max(values.length, 2);
  const x = (i: number) => padL + (i / (n - 1)) * (width - padL - padR);
  const y = (v: number) => padT + (1 - (v - lo) / range) * (height - padT - padB);
  const line = values.map((v, i) => `${i === 0 ? 'M' : 'L'}${x(i).toFixed(1)},${y(v).toFixed(1)}`).join(' ');
  const ticks = 4;
  const id = `l-${color.replace(/[^a-z0-9]/gi, '')}`;
  return (
    <svg viewBox={`0 0 ${width} ${height}`} className="w-full" role="img" aria-label="Line chart">
      <defs><linearGradient id={id} x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stopColor={color} stopOpacity="0.14" /><stop offset="100%" stopColor={color} stopOpacity="0" /></linearGradient></defs>
      {Array.from({ length: ticks + 1 }, (_, i) => {
        const v = lo + (range / ticks) * i;
        return (
          <g key={i}>
            <line x1={padL} x2={width - padR} y1={y(v)} y2={y(v)} stroke="var(--border)" strokeWidth="1" />
            <text x={padL - 5} y={y(v) + 3} textAnchor="end" fontSize="9" fill="var(--faint)">{Math.round(v)}{unit}</text>
          </g>
        );
      })}
      <path d={`${line} L${x(n - 1)},${height - padB} L${x(0)},${height - padB} Z`} fill={`url(#${id})`} />
      <path d={line} fill="none" stroke={color} strokeWidth="2.5" strokeLinejoin="round" />
      {values.map((v, i) => <circle key={i} cx={x(i)} cy={y(v)} r="3" fill={color} />)}
      {labels.map((l, i) => <text key={l} x={x(i)} y={height - 6} textAnchor="middle" fontSize="9" fill="var(--faint)">{l}</text>)}
    </svg>
  );
}

/** Donut chart with a center total. */
export function Donut({ segments, total, size = 160 }: { segments: { label: string; value: number; color: string }[]; total: number; size?: number }) {
  const r = size / 2 - 14;
  const c = 2 * Math.PI * r;
  const sum = segments.reduce((a, s) => a + s.value, 0) || 1;
  let offset = 0;
  return (
    <svg viewBox={`0 0 ${size} ${size}`} width={size} height={size} role="img" aria-label="Distribution">
      <g transform={`rotate(-90 ${size / 2} ${size / 2})`}>
        <circle cx={size / 2} cy={size / 2} r={r} fill="none" stroke="var(--bg)" strokeWidth="14" />
        {segments.map((s) => {
          const len = (s.value / sum) * c;
          const el = (
            <circle key={s.label} cx={size / 2} cy={size / 2} r={r} fill="none" stroke={s.color}
              strokeWidth="14" strokeDasharray={`${len} ${c - len}`} strokeDashoffset={-offset} strokeLinecap="butt" />
          );
          offset += len;
          return el;
        })}
      </g>
      <text x={size / 2} y={size / 2 - 4} textAnchor="middle" fontSize="12" fill="var(--muted)">Total</text>
      <text x={size / 2} y={size / 2 + 16} textAnchor="middle" fontSize="22" fontWeight="700" fill="var(--fg)">{total}</text>
    </svg>
  );
}
