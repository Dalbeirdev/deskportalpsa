'use client';

/** Minimal inline-SVG dual sparkline (created vs resolved) — no external chart dependency. */
export function Sparkline({
  created,
  resolved,
  height = 56,
}: {
  created: number[];
  resolved: number[];
  height?: number;
}) {
  const width = 320;
  const max = Math.max(1, ...created, ...resolved);
  const n = Math.max(created.length, resolved.length, 2);

  const path = (series: number[]) =>
    series
      .map((v, i) => {
        const x = (i / (n - 1)) * width;
        const y = height - (v / max) * (height - 6) - 3;
        return `${i === 0 ? 'M' : 'L'}${x.toFixed(1)},${y.toFixed(1)}`;
      })
      .join(' ');

  return (
    <svg viewBox={`0 0 ${width} ${height}`} className="w-full" role="img" aria-label="Created vs resolved trend">
      <path d={path(created)} fill="none" stroke="var(--faint)" strokeWidth="2" />
      <path d={path(resolved)} fill="none" stroke="var(--brand-line)" strokeWidth="2" />
    </svg>
  );
}
