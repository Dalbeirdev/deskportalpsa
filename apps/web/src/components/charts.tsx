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
