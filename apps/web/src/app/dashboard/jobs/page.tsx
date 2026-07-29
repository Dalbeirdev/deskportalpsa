'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  RotateCw, ListChecks, RefreshCw, Clock, Loader, CheckCircle2, AlertOctagon, Ban,
} from 'lucide-react';
import { api } from '@/lib/api';
import type { Job } from '@/lib/types';

// BackgroundJobStatus enum: 0 Queued, 1 Running, 2 Succeeded, 3 Failed, 4 DeadLettered
const STATUS: Record<number, { label: string; tone: string; dot: string }> = {
  0: { label: 'Queued', tone: 'bg-amber-100 text-amber-700 dark:bg-amber-950 dark:text-amber-300', dot: 'bg-amber-500' },
  1: { label: 'Running', tone: 'bg-blue-100 text-blue-700 dark:bg-blue-950 dark:text-blue-300', dot: 'bg-blue-500' },
  2: { label: 'Succeeded', tone: 'bg-green-100 text-green-700 dark:bg-green-950 dark:text-green-300', dot: 'bg-green-500' },
  3: { label: 'Failed', tone: 'bg-red-100 text-red-700 dark:bg-red-950 dark:text-red-300', dot: 'bg-red-500' },
  4: { label: 'Dead-lettered', tone: 'bg-red-100 text-red-700 dark:bg-red-950 dark:text-red-300', dot: 'bg-red-600' },
};
const num = (s: string | number) => Number(s);
function ago(iso: string): string {
  const d = (Date.now() - new Date(iso).getTime()) / 1000;
  const abs = Math.abs(d);
  const t = abs < 60 ? `${Math.floor(abs)}s` : abs < 3600 ? `${Math.floor(abs / 60)} min` : abs < 86400 ? `${Math.floor(abs / 3600)} hr` : `${Math.floor(abs / 86400)}d`;
  return d >= 0 ? `${t} ago` : `in ${t}`;
}

function StatCard({ icon: Icon, iconTone, label, value }: { icon: React.ElementType; iconTone: string; label: string; value: number }) {
  return (
    <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-4">
      <div className="flex items-center gap-3">
        <span className={`inline-flex h-10 w-10 items-center justify-center rounded-lg ${iconTone}`}><Icon size={18} /></span>
        <div><div className="text-2xl font-semibold tabular-nums leading-tight">{value}</div><div className="text-xs text-[var(--muted)]">{label}</div></div>
      </div>
    </div>
  );
}

export default function JobsPage() {
  const qc = useQueryClient();
  const { data, isLoading, isError } = useQuery({ queryKey: ['jobs'], queryFn: () => api.jobs() });
  const [filter, setFilter] = useState<number | null>(null);

  const reprocess = useMutation({
    mutationFn: (id: string) => api.reprocessJob(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['jobs'] }),
  });

  const jobs = data ?? [];
  const count = (s: number) => jobs.filter((j) => num(j.status) === s).length;
  const shown = filter == null ? jobs : jobs.filter((j) => num(j.status) === filter);

  const TABS: { key: number | null; label: string }[] = [
    { key: null, label: 'All' }, { key: 0, label: 'Queued' }, { key: 1, label: 'Running' },
    { key: 2, label: 'Succeeded' }, { key: 4, label: 'Dead-lettered' },
  ];

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Background Jobs</h1>
          <p className="text-sm text-[var(--muted)]">Monitor sync jobs and reprocess anything that dead-lettered.</p>
        </div>
        <button onClick={() => qc.invalidateQueries({ queryKey: ['jobs'] })} className="inline-flex items-center gap-2 rounded-lg border border-[var(--border)] bg-[var(--surface)] px-3 py-2 text-sm font-medium hover:bg-[var(--bg)]">
          <RefreshCw size={15} /> Refresh
        </button>
      </div>

      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        <StatCard icon={Clock} iconTone="bg-amber-50 text-amber-600 dark:bg-amber-950/50 dark:text-amber-300" label="Queued" value={count(0)} />
        <StatCard icon={Loader} iconTone="bg-blue-50 text-blue-600 dark:bg-blue-950/50 dark:text-blue-300" label="Running" value={count(1)} />
        <StatCard icon={CheckCircle2} iconTone="bg-green-50 text-green-600 dark:bg-green-950/50 dark:text-green-300" label="Succeeded" value={count(2)} />
        <StatCard icon={AlertOctagon} iconTone="bg-red-50 text-red-600 dark:bg-red-950/50 dark:text-red-300" label="Dead-lettered" value={count(4)} />
      </div>

      <div className="inline-flex flex-wrap rounded-lg border border-[var(--border)] bg-[var(--surface)] p-0.5">
        {TABS.map((t) => (
          <button key={String(t.key)} onClick={() => setFilter(t.key)}
            className={`rounded-md px-3 py-1.5 text-sm font-medium ${filter === t.key ? 'bg-brand text-brand-fg' : 'text-[var(--muted)] hover:text-[var(--fg)]'}`}>
            {t.label}
          </button>
        ))}
      </div>

      {isLoading && <div className="h-40 animate-pulse rounded-xl border border-[var(--border)] bg-[var(--surface)]" />}

      {(isError || (data && jobs.length === 0)) && (
        <div className="flex flex-col items-center rounded-xl border border-dashed border-[var(--border)] px-6 py-12 text-center">
          <ListChecks className="mb-3 text-[var(--faint)]" size={26} />
          <p className="text-sm text-[var(--muted)]">No jobs in the queue.</p>
        </div>
      )}

      {jobs.length > 0 && (
        <div className="overflow-x-auto rounded-xl border border-[var(--border)] bg-[var(--surface)]">
          <table className="w-full text-sm">
            <thead className="text-left text-[10px] uppercase tracking-wide text-[var(--faint)]">
              <tr className="border-b border-[var(--border)]">
                <th className="px-5 py-2.5 font-medium">Job Type</th>
                <th className="px-2 py-2.5 font-medium">Status</th>
                <th className="px-2 py-2.5 font-medium">Attempts</th>
                <th className="px-2 py-2.5 font-medium">Next / Created</th>
                <th className="px-5 py-2.5 font-medium">Last Error</th>
                <th className="px-5 py-2.5 text-right font-medium">Actions</th>
              </tr>
            </thead>
            <tbody>
              {shown.map((j: Job) => {
                const s = STATUS[num(j.status)] ?? STATUS[0];
                const dead = num(j.status) === 4;
                return (
                  <tr key={j.id} className="border-b border-[var(--border)] last:border-0">
                    <td className="px-5 py-3 font-medium">{j.jobType}</td>
                    <td className="px-2 py-3"><span className={`inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-xs font-medium ${s.tone}`}><span className={`h-1.5 w-1.5 rounded-full ${s.dot}`} />{s.label}</span></td>
                    <td className="px-2 py-3 tabular-nums text-[var(--muted)]">{j.attempts}/{j.maxAttempts}</td>
                    <td className="px-2 py-3 text-[var(--muted)]">{j.nextAttemptAt ? ago(j.nextAttemptAt) : ago(j.createdAt)}</td>
                    <td className="max-w-xs truncate px-5 py-3 text-[var(--muted)]">{j.lastError ?? '—'}</td>
                    <td className="px-5 py-3 text-right">
                      {dead ? (
                        <button onClick={() => reprocess.mutate(j.id)} disabled={reprocess.isPending}
                          className="inline-flex items-center gap-1.5 rounded-lg border border-[var(--border)] px-2.5 py-1.5 text-xs font-medium hover:bg-[var(--bg)] disabled:opacity-50">
                          <RotateCw size={13} /> Reprocess
                        </button>
                      ) : (
                        <span className="inline-flex items-center gap-1 text-xs text-[var(--faint)]"><Ban size={12} /> —</span>
                      )}
                    </td>
                  </tr>
                );
              })}
              {shown.length === 0 && <tr><td colSpan={6} className="px-5 py-8 text-center text-sm text-[var(--muted)]">No jobs in this view.</td></tr>}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
