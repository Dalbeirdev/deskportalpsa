'use client';

import { useMemo, useState } from 'react';
import {
  MailOpen, Settings, Filter, Ticket, CheckCircle2, AlertTriangle, RefreshCw, UserPlus,
  AlertOctagon, ShieldCheck, Mail, Eye, MoreVertical,
} from 'lucide-react';

type Cat = 'tickets' | 'integrations' | 'system';
type Note = {
  id: number; icon: React.ElementType; iconTone: string; title: string; summary: string;
  time: string; cat: Cat; unread: boolean; badge?: { label: string; tone: string };
};

const NOTES: Note[] = [
  { id: 1, icon: Ticket, iconTone: 'bg-blue-50 text-blue-600 dark:bg-blue-950/50 dark:text-blue-300', title: 'Ticket #2456 assigned', summary: 'Ticket #2456 (Printer not responding) has been assigned to John Doe.', time: '2 min ago', cat: 'tickets', unread: true, badge: { label: 'New', tone: 'bg-blue-100 text-blue-700 dark:bg-blue-950 dark:text-blue-300' } },
  { id: 2, icon: CheckCircle2, iconTone: 'bg-green-50 text-green-600 dark:bg-green-950/50 dark:text-green-300', title: 'Ticket #2455 resolved', summary: 'Ticket #2455 (VPN connection issue) has been resolved by Sarah Lee.', time: '10 min ago', cat: 'tickets', unread: true },
  { id: 3, icon: AlertTriangle, iconTone: 'bg-amber-50 text-amber-600 dark:bg-amber-950/50 dark:text-amber-300', title: 'SLA breach alert', summary: 'Ticket #2448 (Email not syncing) has breached the SLA.', time: '23 min ago', cat: 'tickets', unread: true, badge: { label: 'High', tone: 'bg-amber-100 text-amber-700 dark:bg-amber-950 dark:text-amber-300' } },
  { id: 4, icon: RefreshCw, iconTone: 'bg-blue-50 text-blue-600 dark:bg-blue-950/50 dark:text-blue-300', title: 'Autotask sync completed', summary: '86 records synchronized successfully.', time: '35 min ago', cat: 'integrations', unread: false },
  { id: 5, icon: UserPlus, iconTone: 'bg-violet-50 text-violet-600 dark:bg-violet-950/50 dark:text-violet-300', title: 'New customer added', summary: 'Acme Corporation has been added by Demo Admin.', time: '1 hour ago', cat: 'system', unread: false },
  { id: 6, icon: AlertOctagon, iconTone: 'bg-red-50 text-red-600 dark:bg-red-950/50 dark:text-red-300', title: 'ServiceNow connection warning', summary: 'High response time detected for ServiceNow integration.', time: '1 hour ago', cat: 'integrations', unread: false },
  { id: 7, icon: ShieldCheck, iconTone: 'bg-sky-50 text-sky-600 dark:bg-sky-950/50 dark:text-sky-300', title: 'Integration health recovered', summary: 'HaloPSA Production is healthy again.', time: '2 hours ago', cat: 'integrations', unread: false },
  { id: 8, icon: Mail, iconTone: 'bg-amber-50 text-amber-600 dark:bg-amber-950/50 dark:text-amber-300', title: 'Dead-letter queue', summary: '2 messages moved to dead-letter queue in ConnectWise.', time: '3 hours ago', cat: 'system', unread: false },
  { id: 9, icon: Ticket, iconTone: 'bg-blue-50 text-blue-600 dark:bg-blue-950/50 dark:text-blue-300', title: 'Ticket #2440 reopened', summary: 'Ticket #2440 (Slow laptop) was reopened by the customer.', time: '4 hours ago', cat: 'tickets', unread: true },
  { id: 10, icon: CheckCircle2, iconTone: 'bg-green-50 text-green-600 dark:bg-green-950/50 dark:text-green-300', title: 'Ticket #2431 resolved', summary: 'Ticket #2431 (Password reset) has been resolved by Mike Smith.', time: '5 hours ago', cat: 'tickets', unread: false },
  { id: 11, icon: RefreshCw, iconTone: 'bg-blue-50 text-blue-600 dark:bg-blue-950/50 dark:text-blue-300', title: 'ConnectWise sync completed', summary: '124 records synchronized successfully.', time: '6 hours ago', cat: 'integrations', unread: false },
  { id: 12, icon: ShieldCheck, iconTone: 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300', title: 'Scheduled maintenance', summary: 'Background job runner restarted for nightly maintenance.', time: '8 hours ago', cat: 'system', unread: false },
];

export default function NotificationsPage() {
  const [notes, setNotes] = useState<Note[]>(NOTES);
  const [tab, setTab] = useState<'all' | 'unread' | Cat>('all');

  const counts = useMemo(() => ({
    all: notes.length,
    unread: notes.filter((n) => n.unread).length,
    tickets: notes.filter((n) => n.cat === 'tickets').length,
    integrations: notes.filter((n) => n.cat === 'integrations').length,
    system: notes.filter((n) => n.cat === 'system').length,
  }), [notes]);

  const shown = notes.filter((n) => tab === 'all' ? true : tab === 'unread' ? n.unread : n.cat === tab);

  const markAll = () => setNotes((ns) => ns.map((n) => ({ ...n, unread: false })));
  const markOne = (id: number) => setNotes((ns) => ns.map((n) => (n.id === id ? { ...n, unread: false } : n)));

  const TABS: { key: typeof tab; label: string; count: number }[] = [
    { key: 'all', label: 'All', count: counts.all },
    { key: 'unread', label: 'Unread', count: counts.unread },
    { key: 'tickets', label: 'Tickets', count: counts.tickets },
    { key: 'integrations', label: 'Integrations', count: counts.integrations },
    { key: 'system', label: 'System', count: counts.system },
  ];

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Notifications</h1>
          <p className="text-sm text-[var(--muted)]">Recent activity on your tickets and integrations.</p>
        </div>
        <div className="flex items-center gap-2">
          <button onClick={markAll} className="inline-flex items-center gap-2 rounded-lg border border-[var(--border)] bg-[var(--surface)] px-3 py-2 text-sm font-medium hover:bg-[var(--bg)]">
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
              {t.key === 'unread' && t.count > 0 && tab !== 'unread' && <span className="h-1.5 w-1.5 rounded-full bg-brand" />}
              {t.label}
              <span className={`rounded-full px-1.5 text-xs ${tab === t.key ? 'bg-brand-fg/20' : 'bg-[var(--bg)] text-[var(--muted)]'}`}>{t.count}</span>
            </button>
          ))}
        </div>
        <button className="ml-auto inline-flex items-center gap-2 rounded-lg border border-[var(--border)] bg-[var(--surface)] px-3 py-2 text-sm font-medium hover:bg-[var(--bg)]">
          <Filter size={15} /> Filter
        </button>
      </div>

      <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)]">
        <div className="grid grid-cols-[1fr_auto_auto] gap-4 border-b border-[var(--border)] px-5 py-2.5 text-[10px] uppercase tracking-wide text-[var(--faint)]">
          <span>Notification</span><span className="pr-6">Time</span><span>Actions</span>
        </div>
        <ul>
          {shown.map((n) => {
            const Icon = n.icon;
            return (
              <li key={n.id} className={`grid grid-cols-[1fr_auto_auto] items-center gap-4 border-b border-[var(--border)] px-5 py-3.5 last:border-0 ${n.unread ? 'bg-blue-50/40 dark:bg-blue-950/10' : ''}`}>
                <div className="flex items-start gap-3">
                  <span className="mt-1 w-2 shrink-0">{n.unread && <span className="block h-2 w-2 rounded-full bg-brand" />}</span>
                  <span className={`inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-full ${n.iconTone}`}><Icon size={16} /></span>
                  <div className="min-w-0">
                    <div className="text-sm font-semibold">{n.title}</div>
                    <div className="text-sm text-[var(--muted)]">{n.summary}</div>
                  </div>
                </div>
                <div className="whitespace-nowrap text-right">
                  <div className="text-xs text-[var(--muted)]">{n.time}</div>
                  {n.badge && <span className={`mt-1 inline-flex rounded-full px-2 py-0.5 text-[11px] font-medium ${n.badge.tone}`}>{n.badge.label}</span>}
                </div>
                <div className="flex items-center gap-1">
                  <button onClick={() => markOne(n.id)} aria-label="Mark read" className="rounded-md border border-[var(--border)] p-1.5 text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)]"><Eye size={15} /></button>
                  <button aria-label="More" className="rounded-md border border-[var(--border)] p-1.5 text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)]"><MoreVertical size={15} /></button>
                </div>
              </li>
            );
          })}
          {shown.length === 0 && (
            <li className="px-5 py-12 text-center text-sm text-[var(--muted)]">Nothing here — you&apos;re all caught up.</li>
          )}
        </ul>
      </div>
    </div>
  );
}
