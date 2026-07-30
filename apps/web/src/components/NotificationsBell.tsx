'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Bell } from 'lucide-react';
import { api } from '@/lib/api';

/** Header bell: links to Notifications and badges the count of activity in the last 24h — real
 * data from the notifications feed, no fabricated numbers. Badge hidden when there's nothing. */
export function NotificationsBell() {
  const { data } = useQuery({ queryKey: ['notifications'], queryFn: api.notifications, retry: false });
  const recent = (data ?? []).filter((n) => Date.now() - new Date(n.at).getTime() < 24 * 3600_000).length;

  return (
    <Link href="/dashboard/notifications" aria-label={`Notifications${recent ? ` (${recent} in the last day)` : ''}`}
      className="relative rounded-lg p-2 text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)]">
      <Bell size={18} />
      {recent > 0 && (
        <span className="absolute right-1 top-1 flex h-4 min-w-4 items-center justify-center rounded-full bg-brand px-1 text-[10px] font-semibold text-brand-fg">
          {recent > 9 ? '9+' : recent}
        </span>
      )}
    </Link>
  );
}
