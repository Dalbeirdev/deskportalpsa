'use client';

import { useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ShieldCheck, RefreshCw, Search, Plug, SlidersHorizontal, Ticket, UserPlus, Settings, Activity,
} from 'lucide-react';
import { api } from '@/lib/api';
import type { AuditEntry } from '@/lib/types';

function ago(iso: string): string {
  const s = Math.max(0, Math.floor((Date.now() - new Date(iso).getTime()) / 1000));
  if (s < 60) return `${s}s ago`;
  if (s < 3600) return `${Math.floor(s / 60)} min ago`;
  if (s < 86400) return `${Math.floor(s / 3600)} hr ago`;
  return `${Math.floor(s / 86400)}d ago`;
}
// Pick an icon + tone from the action's namespace prefix.
function decorate(action: string) {
  const p = action.split('.')[0];
  const map: Record<string, { icon: React.ElementType; tone: string }> = {
    connection: { icon: Plug, tone: 'bg-blue-50 text-blue-600 dark:bg-blue-950/50 dark:text-blue-300' },
    mapping: { icon: SlidersHorizontal, tone: 'bg-violet-50 text-violet-600 dark:bg-violet-950/50 dark:text-violet-300' },
    ticket: { icon: Ticket, tone: 'bg-sky-50 text-sky-600 dark:bg-sky-950/50 dark:text-sky-300' },
    clientuser: { icon: UserPlus, tone: 'bg-green-50 text-green-600 dark:bg-green-950/50 dark:text-green-300' },
    user: { icon: UserPlus, tone: 'bg-green-50 text-green-600 dark:bg-green-950/50 dark:text-green-300' },
  };
  return map[p] ?? { icon: Settings, tone: 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300' };
}

export default function AuditPage() {
  const qc = useQueryClient();
  const { data, isLoading, isError } = useQuery({ queryKey: ['audit'], queryFn: api.audit });
  const [q, setQ] = useState('');

  const entries = data ?? [];
  const filtered = useMemo(() => {
    const needle = q.trim().toLowerCase();
    if (!needle) return entries;
    return entries.filter((a) =>
      a.action.toLowerCase().includes(needle) ||
      a.entityType.toLowerCase().includes(needle) ||
      (a.actorDisplayName ?? '').toLowerCase().includes(needle));
  }, [entries, q]);

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Audit Log</h1>
          <p className="text-sm text-[var(--muted)]">Immutable record of administrative and security events.</p>
        </div>
        <div className="flex items-center gap-2">
          <div className="relative">
            <Search size={15} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-[var(--faint)]" />
            <input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Search actions, actors…"
              className="w-56 rounded-lg border border-[var(--border)] bg-[var(--surface)] py-2 pl-9 pr-3 text-sm outline-none focus:border-brand" />
          </div>
          <button onClick={() => qc.invalidateQueries({ queryKey: ['audit'] })} className="inline-flex items-center gap-2 rounded-lg border border-[var(--border)] bg-[var(--surface)] px-3 py-2 text-sm font-medium hover:bg-[var(--bg)]">
            <RefreshCw size={15} /> Refresh
          </button>
        </div>
      </div>

      <div className="flex items-center gap-2 rounded-lg border border-[var(--border)] bg-[var(--surface)] px-4 py-2.5 text-sm text-[var(--muted)]">
        <ShieldCheck size={16} className="text-brand" />
        <span><strong className="text-[var(--fg)]">{entries.length}</strong> events · append-only, tamper-evident trail</span>
      </div>

      {isLoading && <div className="h-40 animate-pulse rounded-xl border border-[var(--border)] bg-[var(--surface)]" />}

      {(isError || (data && entries.length === 0)) && (
        <div className="flex flex-col items-center rounded-xl border border-dashed border-[var(--border)] px-6 py-12 text-center">
          <Activity className="mb-3 text-[var(--faint)]" size={26} />
          <p className="text-sm text-[var(--muted)]">No audit entries yet.</p>
        </div>
      )}

      {entries.length > 0 && (
        <div className="overflow-x-auto rounded-xl border border-[var(--border)] bg-[var(--surface)]">
          <table className="w-full text-sm">
            <thead className="text-left text-[10px] uppercase tracking-wide text-[var(--faint)]">
              <tr className="border-b border-[var(--border)]">
                <th className="px-5 py-2.5 font-medium">Action</th>
                <th className="px-2 py-2.5 font-medium">Entity</th>
                <th className="px-2 py-2.5 font-medium">Actor</th>
                <th className="px-5 py-2.5 font-medium">When</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((a: AuditEntry) => {
                const d = decorate(a.action);
                const Icon = d.icon;
                return (
                  <tr key={a.id} className="border-b border-[var(--border)] last:border-0">
                    <td className="px-5 py-3">
                      <span className="flex items-center gap-2.5">
                        <span className={`inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-lg ${d.tone}`}><Icon size={15} /></span>
                        <span className="font-medium">{a.action}</span>
                      </span>
                    </td>
                    <td className="px-2 py-3 text-[var(--muted)]">{a.entityType}{a.entityId ? ` #${a.entityId.slice(0, 8)}` : ''}</td>
                    <td className="px-2 py-3">{a.actorDisplayName ?? '—'}</td>
                    <td className="px-5 py-3 text-[var(--muted)]" title={new Date(a.createdAt).toLocaleString()}>{ago(a.createdAt)}</td>
                  </tr>
                );
              })}
              {filtered.length === 0 && <tr><td colSpan={4} className="px-5 py-8 text-center text-sm text-[var(--muted)]">No entries match “{q}”.</td></tr>}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
