'use client';

import { useQuery } from '@tanstack/react-query';
import { Download, Info } from 'lucide-react';
import { api } from '@/lib/api';
import { Sparkline } from '@/components/Sparkline';

const DISCLAIMER =
  'Productivity scores are operational indicators only and must not be used as the sole basis for employee performance decisions.';

export default function AnalyticsPage() {
  const tech = useQuery({ queryKey: ['dash-tech'], queryFn: api.technicianMetrics });
  const team = useQuery({ queryKey: ['dash-team'], queryFn: api.teamMetrics });
  const trend = useQuery({ queryKey: ['dash-trend'], queryFn: api.trend });

  const m = tech.data?.metrics;
  const score = m?.score;

  const tiles = [
    { label: 'Assigned', value: m?.assigned },
    { label: 'Resolved', value: m?.resolved },
    { label: 'Open', value: m?.open },
    { label: 'Overdue', value: m?.overdue },
    { label: 'SLA compliance', value: m ? `${m.slaCompliancePct}%` : undefined },
    { label: 'Time worked', value: m ? `${m.timeWorkedHours}h` : undefined },
  ];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">Productivity</h1>
          <p className="text-sm text-[var(--muted)]">Technician and team performance across connected systems.</p>
        </div>
        <a
          href={api.teamExportUrl}
          className="inline-flex items-center gap-2 rounded-lg border border-[var(--border)] bg-[var(--surface)] px-3.5 py-2 text-sm font-medium hover:bg-[var(--bg)]"
        >
          <Download size={16} /> Export CSV
        </a>
      </div>

      {/* Guardrail — shown regardless of data */}
      <div className="flex items-start gap-2.5 rounded-lg border border-amber-300/60 bg-amber-50 px-4 py-3 text-sm text-amber-800 dark:border-amber-900/60 dark:bg-amber-950/40 dark:text-amber-300">
        <Info size={16} className="mt-0.5 shrink-0" />
        <span>{tech.data?.disclaimer ?? DISCLAIMER}</span>
      </div>

      {/* Score + tiles */}
      <div className="grid gap-4 lg:grid-cols-3">
        <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-6 lg:col-span-1">
          <div className="text-sm text-[var(--muted)]">Productivity score</div>
          <div className="mt-1 flex items-baseline gap-2">
            <span className="text-4xl font-semibold tabular-nums">{score ? score.overall.toFixed(1) : '—'}</span>
            <span className="text-sm text-[var(--muted)]">/ 100</span>
          </div>
          {score && (
            <>
              <div className="mt-1 text-xs text-[var(--faint)]">
                Based on {Math.round(score.measuredWeightFraction * 100)}% of the weighted model (measured signals only).
              </div>
              <div className="mt-4 space-y-2">
                {score.breakdown.map((b) => (
                  <div key={b.component}>
                    <div className="flex justify-between text-xs">
                      <span>{b.component}</span>
                      <span className="tabular-nums text-[var(--muted)]">{b.score.toFixed(0)}</span>
                    </div>
                    <div className="mt-1 h-1.5 rounded-full bg-[var(--bg)]">
                      <div className="h-1.5 rounded-full bg-brand" style={{ width: `${Math.min(100, b.score)}%` }} />
                    </div>
                  </div>
                ))}
              </div>
            </>
          )}
          {!score && <div className="mt-3 text-sm text-[var(--muted)]">Sign in to load metrics. Preview runs without a backend.</div>}
        </div>

        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:col-span-2">
          {tiles.map((t) => (
            <div key={t.label} className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-4">
              <div className="text-sm text-[var(--muted)]">{t.label}</div>
              <div className="mt-1.5 text-2xl font-semibold tabular-nums">{t.value ?? '—'}</div>
            </div>
          ))}
        </div>
      </div>

      {/* Trend */}
      <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-6">
        <div className="mb-3 flex items-center justify-between">
          <h2 className="text-sm font-semibold">Created vs resolved</h2>
          <div className="flex gap-4 text-xs text-[var(--muted)]">
            <span className="flex items-center gap-1.5"><i className="h-0.5 w-4" style={{ background: 'var(--faint)' }} /> Created</span>
            <span className="flex items-center gap-1.5"><i className="h-0.5 w-4" style={{ background: 'var(--brand-line)' }} /> Resolved</span>
          </div>
        </div>
        {trend.data && trend.data.length > 0 ? (
          <Sparkline created={trend.data.map((p) => p.created)} resolved={trend.data.map((p) => p.resolved)} />
        ) : (
          <p className="py-6 text-center text-sm text-[var(--muted)]">No trend data for this period.</p>
        )}
      </div>

      {/* Team comparison */}
      <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] overflow-hidden">
        <h2 className="border-b border-[var(--border)] px-6 py-3 text-sm font-semibold">Team comparison</h2>
        {team.data && team.data.team.length > 0 ? (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="text-left text-xs uppercase tracking-wide text-[var(--muted)]">
                <tr className="border-b border-[var(--border)]">
                  <th className="px-6 py-2.5 font-medium">Technician</th>
                  <th className="px-6 py-2.5 font-medium">Resolved</th>
                  <th className="px-6 py-2.5 font-medium">SLA %</th>
                  <th className="px-6 py-2.5 font-medium">Score</th>
                </tr>
              </thead>
              <tbody>
                {team.data.team.map((r) => (
                  <tr key={r.technicianExternalId} className="border-b border-[var(--border)] last:border-0">
                    <td className="px-6 py-2.5 font-medium">{r.technicianExternalId}</td>
                    <td className="px-6 py-2.5 tabular-nums">{r.resolved}</td>
                    <td className="px-6 py-2.5 tabular-nums">{r.slaCompliancePct}%</td>
                    <td className="px-6 py-2.5 tabular-nums">{r.score?.toFixed(1) ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <p className="px-6 py-8 text-center text-sm text-[var(--muted)]">No team data to compare yet.</p>
        )}
      </div>
    </div>
  );
}
