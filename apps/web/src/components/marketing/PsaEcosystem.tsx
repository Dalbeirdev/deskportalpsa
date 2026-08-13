import { Check, Clock3, ArrowRight } from 'lucide-react';
import { PSA_PLATFORMS, AVAILABLE_PLATFORMS, STATUS_LABEL, type PsaPlatform } from '@/lib/psaPlatforms';

/**
 * One client experience, many PSAs — drawn as a hub with a spoke to each platform.
 *
 * A row of identical logo boxes says "we integrate with things". A hub says the portal is the
 * single surface and the PSA is a choice underneath it, which is the actual positioning.
 *
 * Marks are initials, not third-party logos: we have no licence to redistribute vendor brand
 * assets, and a wrong-looking logo damages more trust than no logo.
 */
export function PsaEcosystem() {
  const cols = PSA_PLATFORMS.length;
  const width = 1000;
  const hubX = width / 2;
  const slot = width / cols;

  return (
    <>
      <svg
        viewBox="0 0 1000 300"
        className="hidden h-auto w-full lg:block"
        role="img"
        aria-label={`Desk Portal connects to ${cols} PSA platforms: ${PSA_PLATFORMS.map((p) => `${p.name}, ${STATUS_LABEL[p.status].toLowerCase()}`).join('; ')}.`}
      >
        <rect x={hubX - 110} y="20" width="220" height="64" rx="16" fill="var(--brand-line)" />
        <text x={hubX} y="48" textAnchor="middle" fontSize="15" fontWeight="600" fill="var(--surface)">
          Desk Portal
        </text>
        <text x={hubX} y="68" textAnchor="middle" fontSize="11" fill="var(--surface)" fillOpacity="0.75">
          One client experience
        </text>

        {PSA_PLATFORMS.map((p, i) => {
          const x = slot * i + slot / 2;
          const live = p.status === 'available';
          return (
            <g key={p.id}>
              <path
                d={`M ${hubX} 84 C ${hubX} 150, ${x} 140, ${x} 196`}
                fill="none"
                stroke="var(--brand-line)"
                strokeWidth={live ? 2 : 1.25}
                strokeOpacity={live ? 0.85 : 0.3}
                strokeDasharray={live ? undefined : '4 5'}
              />
              {live && (
                <circle r="3.5" fill="var(--brand-line)" className="dp-travel"
                  style={{ offsetPath: `path('M ${hubX} 84 C ${hubX} 150, ${x} 140, ${x} 196')`, animationDelay: `${-i * 0.5}s` }} />
              )}
              <rect
                x={x - 54} y={196} width="108" height="72" rx="14"
                fill="var(--surface)" stroke="var(--border)"
              />
              <circle cx={x} cy={220} r="13" fill="var(--brand-line)" fillOpacity={live ? 0.14 : 0.07} />
              <text x={x} y={225} textAnchor="middle" fontSize="10.5" fontWeight="700" fill="var(--brand-line)" fillOpacity={live ? 1 : 0.55}>
                {p.initials}
              </text>
              <text x={x} y={247} textAnchor="middle" fontSize="9.5" fontWeight="600" fill="var(--fg)">
                {p.name.length > 16 ? `${p.name.slice(0, 15)}…` : p.name}
              </text>
              <text x={x} y={260} textAnchor="middle" fontSize="8.5" fill={live ? 'var(--brand-line)' : 'var(--faint)'}>
                {STATUS_LABEL[p.status]}
              </text>
            </g>
          );
        })}
      </svg>

      <div className="lg:hidden">
        <div className="mx-auto mb-5 w-max rounded-xl bg-brand px-5 py-3 text-center text-brand-fg">
          <p className="text-sm font-semibold">Desk Portal</p>
          <p className="text-[11px] text-brand-fg/75">One client experience</p>
        </div>
        <PsaGrid />
      </div>
    </>
  );
}

function Tile({ p }: { p: PsaPlatform }) {
  const live = p.status === 'available';
  return (
    <div
      className={`dp-lift flex h-full flex-col rounded-2xl border p-4 ${
        live ? 'border-brand/30 bg-[var(--surface)]' : 'border-dashed border-[var(--border)] bg-[var(--bg)]'
      }`}
    >
      <div className="flex items-center gap-2.5">
        <span
          className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-xl text-[12px] font-bold ${
            live
              ? 'bg-brand text-brand-fg'
              : 'bg-[var(--surface)] text-[var(--faint)] ring-1 ring-[var(--border)]'
          }`}
          aria-hidden="true"
        >
          {p.initials}
        </span>
        <span className="min-w-0">
          <span className="block truncate text-[13.5px] font-semibold">{p.name}</span>
          <span
            className={`mt-0.5 inline-flex items-center gap-1 text-[11px] font-medium ${
              live ? 'text-brand-mid' : 'text-[var(--faint)]'
            }`}
          >
            {live ? <Check size={11} aria-hidden="true" /> : <Clock3 size={11} aria-hidden="true" />}
            {STATUS_LABEL[p.status]}
          </span>
        </span>
      </div>
      <p className="mt-3 text-[12.5px] leading-relaxed text-[var(--muted)]">{p.blurb}</p>
    </div>
  );
}

export function PsaGrid() {
  return (
    <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
      {PSA_PLATFORMS.map((p) => <Tile key={p.id} p={p} />)}
    </div>
  );
}

/** Honest summary line. States what is live without hiding what is not. */
export function PsaStatusNote() {
  return (
    <p className="mt-5 text-[13px] text-[var(--muted)]">
      {AVAILABLE_PLATFORMS.map((p) => p.name).join(' and ')} are available today and running against
      live instances. The rest are on the roadmap — the connector layer is shared, so each new
      platform reuses the same sync, mapping and client experience.{' '}
      <a href="/contact" className="inline-flex items-center gap-1 text-brand underline underline-offset-2">
        Tell us which you run <ArrowRight size={12} aria-hidden="true" />
      </a>
    </p>
  );
}
