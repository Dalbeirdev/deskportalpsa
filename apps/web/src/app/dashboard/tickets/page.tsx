'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Plus, Inbox } from 'lucide-react';
import { api } from '@/lib/api';
import { StatusBadge, PriorityBadge } from '@/components/badges';

export default function TicketsPage() {
  const { data, isLoading, isError } = useQuery({ queryKey: ['tickets'], queryFn: api.listTickets });

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">Tickets</h1>
          <p className="text-sm text-[var(--muted)]">Your support requests across all connected systems.</p>
        </div>
        <Link
          href="/dashboard/tickets/new"
          className="inline-flex items-center gap-2 rounded-lg bg-brand px-3.5 py-2 text-sm font-medium text-brand-fg hover:opacity-90"
        >
          <Plus size={16} /> New ticket
        </Link>
      </div>

      {isLoading && <SkeletonTable />}

      {isError && (
        <EmptyState
          title="No tickets to show"
          body="Connect your PSA under PSA Connections, then run a sync to load tickets. They appear here after the first successful sync."
        />
      )}

      {!isError && data && data.length === 0 && (
        <EmptyState title="No tickets yet" body="Create a ticket, or run a sync from PSA Connections to pull them from your PSA." />
      )}

      {!isError && data && data.length > 0 && (
        <div className="overflow-x-auto rounded-xl border border-[var(--border)] bg-[var(--surface)]">
          <table className="w-full text-sm">
            <thead className="text-left text-xs uppercase tracking-wide text-[var(--muted)]">
              <tr className="border-b border-[var(--border)]">
                <th className="px-4 py-3 font-medium">Title</th>
                <th className="px-4 py-3 font-medium">Status</th>
                <th className="px-4 py-3 font-medium">Priority</th>
                <th className="px-4 py-3 font-medium">Queue</th>
                <th className="px-4 py-3 font-medium">Created</th>
              </tr>
            </thead>
            <tbody>
              {data.map((t) => (
                <tr key={t.id} className="border-b border-[var(--border)] last:border-0 hover:bg-[var(--bg)]">
                  <td className="px-4 py-3">
                    <Link href={`/dashboard/tickets/${t.id}`} className="font-medium hover:underline">
                      {t.title}
                    </Link>
                    {t.externalTicketId && (
                      <span className="ml-2 text-xs text-[var(--muted)]">#{t.externalTicketId}</span>
                    )}
                  </td>
                  <td className="px-4 py-3"><StatusBadge status={t.portalStatus} /></td>
                  <td className="px-4 py-3"><PriorityBadge priority={t.portalPriority} /></td>
                  <td className="px-4 py-3 text-[var(--muted)]">{t.queueOrBoard ?? '—'}</td>
                  <td className="px-4 py-3 text-[var(--muted)]">{new Date(t.createdAt).toLocaleDateString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function SkeletonTable() {
  return (
    <div className="space-y-2">
      {[0, 1, 2, 3].map((i) => (
        <div key={i} className="h-12 animate-pulse rounded-lg bg-[var(--surface)] border border-[var(--border)]" />
      ))}
    </div>
  );
}

function EmptyState({ title, body }: { title: string; body: string }) {
  return (
    <div className="flex flex-col items-center rounded-xl border border-dashed border-[var(--border)] px-6 py-14 text-center">
      <Inbox className="mb-3 text-[var(--faint)]" size={28} />
      <h3 className="font-medium">{title}</h3>
      <p className="mt-1 max-w-sm text-sm text-[var(--muted)]">{body}</p>
    </div>
  );
}
