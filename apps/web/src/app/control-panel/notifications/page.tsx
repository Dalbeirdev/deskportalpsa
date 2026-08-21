'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Bell, TicketPlus, MessageSquare, MessagesSquare, CheckCircle2 } from 'lucide-react';
import { api } from '@/lib/api';
import { CpHeader, AccessError } from '../_ui';

/**
 * The client's notification history: a dated feed of what actually happened on their visible
 * tickets — created, replied to (public replies only, both directions), resolved. Every entry is
 * derived from a real record; there is no "notification" table to drift out of sync with reality.
 */

const KIND: Record<string, { icon: React.ElementType; label: (actor: string | null) => string; tone: string }> = {
  'ticket-created': { icon: TicketPlus, label: () => 'Ticket opened', tone: 'bg-blue-100 text-blue-700 dark:bg-blue-950 dark:text-blue-300' },
  'client-reply': { icon: MessageSquare, label: (a) => `${a ?? 'You'} replied`, tone: 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300' },
  'staff-reply': { icon: MessagesSquare, label: (a) => `${a ?? 'Support'} replied`, tone: 'bg-green-100 text-green-700 dark:bg-green-950 dark:text-green-300' },
  'ticket-resolved': { icon: CheckCircle2, label: () => 'Ticket resolved', tone: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300' },
};

function fmt(iso: string) {
  const d = new Date(iso);
  return isNaN(d.getTime()) ? iso : d.toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' });
}

export default function NotificationHistoryPage() {
  const { data, isLoading, error } = useQuery({ queryKey: ['cp-notification-history'], queryFn: api.notificationHistory, retry: false });

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <CpHeader icon={Bell} title="Notification History" subtitle="Everything that has happened on your tickets — opened, replied to, resolved — newest first." />

      {error ? <AccessError label="Notification History" /> : (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)]">
          {isLoading && <div className="px-5 py-8 text-center text-sm text-[var(--muted)]">Loading…</div>}
          {data?.length === 0 && <div className="px-5 py-8 text-center text-sm text-[var(--muted)]">Nothing yet — activity appears here as your tickets move.</div>}
          <ul className="divide-y divide-[var(--border)]">
            {data?.map((e, i) => {
              const k = KIND[e.kind] ?? KIND['ticket-created'];
              return (
                <li key={`${e.ticketId}-${e.kind}-${e.at}-${i}`}>
                  <Link href={`/dashboard/tickets/${e.ticketId}`} className="flex items-center gap-3 px-5 py-3 hover:bg-[var(--bg)]">
                    <span className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-lg ${k.tone}`}><k.icon size={15} /></span>
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-sm font-medium">{e.ticketTitle}</span>
                      <span className="block text-xs text-[var(--muted)]">{k.label(e.actor)}</span>
                    </span>
                    <span className="shrink-0 text-xs text-[var(--faint)]">{fmt(e.at)}</span>
                  </Link>
                </li>
              );
            })}
          </ul>
        </div>
      )}
    </div>
  );
}
