'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Bell } from 'lucide-react';
import { api } from '@/lib/api';

export default function NotificationsPage() {
  const { data, isLoading, isError } = useQuery({ queryKey: ['notifications'], queryFn: api.notifications });

  return (
    <div className="mx-auto max-w-2xl space-y-5">
      <div>
        <h1 className="text-xl font-semibold">Notifications</h1>
        <p className="text-sm text-[var(--muted)]">Recent activity on your tickets.</p>
      </div>

      {isLoading && <div className="h-24 animate-pulse rounded-xl border border-[var(--border)] bg-[var(--surface)]" />}

      {(isError || (data && data.length === 0)) && (
        <div className="flex flex-col items-center rounded-xl border border-dashed border-[var(--border)] px-6 py-12 text-center">
          <Bell className="mb-3 text-[var(--faint)]" size={26} />
          <p className="text-sm text-[var(--muted)]">You&apos;re all caught up.</p>
        </div>
      )}

      {data && data.length > 0 && (
        <ul className="space-y-2">
          {data.map((n) => (
            <li key={`${n.ticketId}-${n.at}`}>
              <Link
                href={`/dashboard/tickets/${n.ticketId}`}
                className="flex items-start gap-3 rounded-lg border border-[var(--border)] bg-[var(--surface)] p-4 hover:bg-[var(--bg)]"
              >
                <Bell size={16} className="mt-0.5 text-brand" />
                <div>
                  <div className="text-sm font-medium">{n.title}</div>
                  <div className="text-sm text-[var(--muted)]">{n.summary}</div>
                  <div className="mt-0.5 text-xs text-[var(--faint)]">{new Date(n.at).toLocaleString()}</div>
                </div>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
