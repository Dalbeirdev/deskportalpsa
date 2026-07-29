'use client';

import Link from 'next/link';
import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  MailOpen, Settings, Filter, Ticket, CheckCircle2, AlertTriangle, Clock, PauseCircle, Bell, Eye,
} from 'lucide-react';
import { api } from '@/lib/api';
import type { Notification } from '@/lib/types';

function ago(iso: string): string {
  const s = Math.max(0, Math.floor((Date.now() - new Date(iso).getTime()) / 1000));
  if (s < 60) return `${s}s ago`;
  if (s < 3600) return `${Math.floor(s / 60)} min ago`;
  if (s < 86400) return `${Math.floor(s / 3600)} hour${Math.floor(s / 3600) === 1 ? '' : 's'} ago`;
  return `${Math.floor(s / 86400)} day${Math.floor(s / 86400) === 1 ? '' : 's'} ago`;
}

// Derive an icon + tone + label from the status carried in the notification summary ("Status: X").
function decorate(n: Notification) {
  const status = (n.summary.split(':')[1] ?? '').trim().toUpperCase();
  const map: Record<string, { icon: React.ElementType; tone: string; label: string; badge?: string }> = {
    RESOLVED: { icon: CheckCircle2, tone: 'bg-green-50 text-green-600 dark:bg-green-950/50 dark:text-green-300', label: 'Resolved' },
    CLOSED: { icon: CheckCircle2, tone: 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300', label: 'Closed' },
    IN_PROGRESS: { icon: Ticket, tone: 'bg-blue-50 text-blue-600 dark:bg-blue-950/50 dark:text-blue-300', label: 'In Progress' },
    NEW: { icon: Ticket, tone: 'bg-blue-50 text-blue-600 dark:bg-blue-950/50 dark:text-blue-300', label: 'New', badge: 'New' },
    ON_HOLD: { icon: PauseCircle, tone: 'bg-orange-50 text-orange-600 dark:bg-orange-950/50 dark:text-orange-300', label: 'On Hold' },
    WAITING_CUSTOMER: { icon: Clock, tone: 'bg-violet-50 text-violet-600 dark:bg-violet-950/50 dark:text-violet-300', label: 'Waiting on Customer' },
  };
  return map[status] ?? { icon: AlertTriangle, tone: 'bg-amber-50 text-amber-600 dark:bg-amber-950/50 dark:text-amber-300', label: status || 'Updated' };
}

export default function NotificationsPage() {
  const { data, isLoading, isError } = useQuery({ queryKey: ['notifications'], queryFn: api.notifications });
  const [tab, setTab] = useState<'all' | 'open' | 'resolved'>('all');
  const [read, setRead] = useState<Set<string>>(new Set());

  const items = useMemo(() => data ?? [], [data]);
  const isResolved = (n: Notification) => /RESOLVED|CLOSED/i.test(n.summary);
  const counts = {
    all: items.length,
    open: items.filter((n) => !isResolved(n)).length,
    resolved: items.filter(isResolved).length,
    unread: items.filter((n) => !read.has(n.ticketId)).length,
  };
  const shown = items.filter((n) => tab === 'all' ? true : tab === 'resolved' ? isResolved(n) : !isResolved(n));

  const TABS: { key: typeof tab; label: string; count: number }[] = [
    { key: 'all', label: 'All', count: counts.all },
    { key: 'open', label: 'Open', count: counts.open },
    { key: 'resolved', label: 'Resolved', count: counts.resolved },
  ];

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Notifications</h1>
          <p className="text-sm text-[var(--muted)]">Recent activity on your tickets and integrations.</p>
        </div>
        <div className="flex items-center gap-2">
          <button onClick={() => setRead(new Set(items.map((n) => n.ticketId)))}
            className="inline-flex items-center gap-2 rounded-lg border border-[var(--border)] bg-[var(--surface)] px-3 py-2 text-sm font-medium hover:bg-[var(--bg)]">
            <MailOpen size={15} /> Mark all as read
          </button>
          <button aria-label="Notification settings" className="rounded-lg border border-[var(--border)] bg-[var(--surface)] p-2 text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)]">
            <Settings size={16} />
          </button>
        </div>
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <div className="inline-flex flex-wrap rounded-lg border border-[var(--border)] bg-[var(--surface)] p-0.5">
          {TABS.map((t) => (
            <button key={t.key} onClick={() => setTab(t.key)}
              className={`inline-flex items-center gap-1.5 rounded-md px-3 py-1.5 text-sm font-medium ${tab === t.key ? 'bg-brand text-brand-fg' : 'text-[var(--muted)] hover:text-[var(--fg)]'}`}>
              {t.label}
              <span className={`rounded-full px-1.5 text-xs ${tab === t.key ? 'bg-brand-fg/20' : 'bg-[var(--bg)] text-[var(--muted)]'}`}>{t.count}</span>
            </button>
          ))}
        </div>
        <button className="ml-auto inline-flex items-center gap-2 rounded-lg border border-[var(--border)] bg-[var(--surface)] px-3 py-2 text-sm font-medium hover:bg-[var(--bg)]">
          <Filter size={15} /> Filter
        </button>
      </div>

      {isLoading && <div className="h-40 animate-pulse rounded-xl border border-[var(--border)] bg-[var(--surface)]" />}

      {(isError || (data && items.length === 0)) && (
        <div className="flex flex-col items-center rounded-xl border border-dashed border-[var(--border)] px-6 py-12 text-center">
          <Bell className="mb-3 text-[var(--faint)]" size={26} />
          <p className="text-sm text-[var(--muted)]">You&apos;re all caught up.</p>
        </div>
      )}

      {items.length > 0 && (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)]">
          <ul>
            {shown.map((n) => {
              const d = decorate(n);
              const Icon = d.icon;
              const unread = !read.has(n.ticketId);
              return (
                <li key={n.ticketId} className={`grid grid-cols-[1fr_auto] items-center gap-4 border-b border-[var(--border)] px-5 py-3.5 last:border-0 ${unread ? 'bg-blue-50/40 dark:bg-blue-950/10' : ''}`}>
                  <Link href={`/dashboard/tickets/${n.ticketId}`} onClick={() => setRead((r) => new Set(r).add(n.ticketId))} className="flex items-start gap-3">
                    <span className="mt-1 w-2 shrink-0">{unread && <span className="block h-2 w-2 rounded-full bg-brand" />}</span>
                    <span className={`inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-full ${d.tone}`}><Icon size={16} /></span>
                    <div className="min-w-0">
                      <div className="text-sm font-semibold">{n.title}</div>
                      <div className="text-sm text-[var(--muted)]">{d.label}</div>
                    </div>
                  </Link>
                  <div className="flex items-center gap-3 whitespace-nowrap">
                    <div className="text-right">
                      <div className="text-xs text-[var(--muted)]">{ago(n.at)}</div>
                      {d.badge && <span className="mt-1 inline-flex rounded-full bg-blue-100 px-2 py-0.5 text-[11px] font-medium text-blue-700 dark:bg-blue-950 dark:text-blue-300">{d.badge}</span>}
                    </div>
                    <button onClick={() => setRead((r) => new Set(r).add(n.ticketId))} aria-label="Mark read" className="rounded-md border border-[var(--border)] p-1.5 text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)]"><Eye size={15} /></button>
                  </div>
                </li>
              );
            })}
            {shown.length === 0 && <li className="px-5 py-12 text-center text-sm text-[var(--muted)]">Nothing in this view.</li>}
          </ul>
        </div>
      )}
    </div>
  );
}
