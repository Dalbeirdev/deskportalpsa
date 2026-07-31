'use client';

import { useQuery } from '@tanstack/react-query';
import { BarChart3, Ticket, FolderOpen, Clock, DollarSign } from 'lucide-react';
import { api } from '@/lib/api';
import { CpHeader, AccessError } from '../_ui';

const STATUS_TONE: Record<string, string> = {
  NEW: 'bg-blue-500', IN_PROGRESS: 'bg-amber-500', WAITING_CUSTOMER: 'bg-violet-500',
  ON_HOLD: 'bg-orange-500', RESOLVED: 'bg-green-500', CLOSED: 'bg-slate-400',
};
const tone = (s: string) => STATUS_TONE[s.toUpperCase()] ?? 'bg-slate-400';
const fmt = (iso: string) => new Date(iso).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });

export default function ReportsPage() {
  const { data, isLoading, error } = useQuery({ queryKey: ['cp-report'], queryFn: api.cpReport, retry: false });
  const max = Math.max(1, ...(data?.byStatus.map((s) => s.count) ?? [1]));

  return (
    <div className="mx-auto max-w-4xl space-y-5">
      <CpHeader icon={BarChart3} title="Reports" subtitle="A summary of your tickets and time — updated as your tickets sync from the service desk." />

      {error ? <AccessError label="Reports" /> : isLoading ? (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-8 text-center text-sm text-[var(--muted)]">Loading…</div>
      ) : data && (
        <>
          <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
            <Stat icon={Ticket} tone="bg-blue-50 text-blue-600 dark:bg-blue-950/50 dark:text-blue-300" label="Total tickets" value={data.totalTickets} />
            <Stat icon={FolderOpen} tone="bg-amber-50 text-amber-600 dark:bg-amber-950/50 dark:text-amber-300" label="Open" value={data.openTickets} />
            <Stat icon={Clock} tone="bg-violet-50 text-violet-600 dark:bg-violet-950/50 dark:text-violet-300" label="Hours logged" value={data.hoursLogged.toFixed(2)} />
            <Stat icon={DollarSign} tone="bg-green-50 text-green-600 dark:bg-green-950/50 dark:text-green-300" label="Billable hours" value={data.billableHours.toFixed(2)} />
          </div>

          <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5">
            <h2 className="mb-4 text-sm font-semibold">Tickets by status</h2>
            {data.byStatus.length === 0 ? <p className="text-sm text-[var(--muted)]">No tickets yet.</p> : (
              <div className="space-y-2.5">
                {data.byStatus.map((s) => (
                  <div key={s.status} className="flex items-center gap-3">
                    <span className="w-36 shrink-0 text-xs font-medium text-[var(--muted)]">{s.status.replace(/_/g, ' ')}</span>
                    <div className="h-5 flex-1 overflow-hidden rounded bg-[var(--bg)]">
                      <div className={`h-full rounded ${tone(s.status)}`} style={{ width: `${(s.count / max) * 100}%` }} />
                    </div>
                    <span className="w-8 shrink-0 text-right text-sm font-semibold tabular-nums">{s.count}</span>
                  </div>
                ))}
              </div>
            )}
          </div>

          <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)]">
            <h2 className="border-b border-[var(--border)] px-5 py-3.5 text-sm font-semibold">Recent tickets</h2>
            <div className="divide-y divide-[var(--border)]">
              {data.recent.length === 0 && <div className="px-5 py-8 text-center text-sm text-[var(--muted)]">No tickets yet.</div>}
              {data.recent.map((t) => (
                <div key={t.id} className="flex items-center gap-3 px-5 py-3">
                  <span className={`h-2 w-2 shrink-0 rounded-full ${tone(t.portalStatus)}`} />
                  <span className="w-20 shrink-0 truncate text-xs text-[var(--muted)]">{t.externalTicketId ?? '—'}</span>
                  <span className="min-w-0 flex-1 truncate text-sm font-medium">{t.title}</span>
                  <span className="shrink-0 text-xs text-[var(--muted)]">{t.portalStatus.replace(/_/g, ' ')}</span>
                  <span className="hidden shrink-0 text-xs text-[var(--faint)] sm:inline">{fmt(t.createdAt)}</span>
                </div>
              ))}
            </div>
          </div>
        </>
      )}
    </div>
  );
}

function Stat({ icon: Icon, tone, label, value }: { icon: React.ElementType; tone: string; label: string; value: React.ReactNode }) {
  return (
    <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-4">
      <span className={`inline-flex h-9 w-9 items-center justify-center rounded-lg ${tone}`}><Icon size={17} /></span>
      <div className="mt-2 text-2xl font-semibold leading-tight tabular-nums">{value}</div>
      <div className="text-xs text-[var(--muted)]">{label}</div>
    </div>
  );
}
