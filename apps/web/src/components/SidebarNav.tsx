'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import {
  LayoutDashboard, Ticket, Plug, Bell, User, BarChart3, Activity, ListChecks, ShieldCheck,
  SlidersHorizontal, HardDrive, Rocket, type LucideIcon,
} from 'lucide-react';
import { api } from '@/lib/api';

// `permissions` is ANY-OF: "Productivity" is rightly visible to a technician who may only see
// their own numbers AND to a manager who holds only the team-wide permission.
type NavItem = { href: string; label: string; icon: LucideIcon; permissions?: string[] };

/**
 * Navigation filtered to what the signed-in user can actually open. A technician without
 * connection rights used to see PSA Connections, Field Mapping and Audit Log anyway — every click
 * a 403. Menus are the first statement of what a role can do; this makes that statement true.
 *
 * Permission keys mirror the [RequirePermission] on each page's backing endpoints, so the menu and
 * the API can't disagree. Items with no key (Tickets, Profile…) are for everyone.
 */
const NAV_GROUPS: { label: string | null; items: NavItem[] }[] = [
  {
    label: null,
    items: [
      { href: '/dashboard', label: 'Overview', icon: LayoutDashboard },
      { href: '/dashboard/tickets', label: 'Tickets', icon: Ticket },
      { href: '/dashboard/analytics', label: 'Productivity', icon: BarChart3, permissions: ['productivity.own.view', 'productivity.team.view'] },
    ],
  },
  {
    label: 'Integrations',
    items: [
      { href: '/dashboard/connections', label: 'PSA Connections', icon: Plug, permissions: ['connections.view'] },
      { href: '/dashboard/mappings', label: 'Field Mapping', icon: SlidersHorizontal, permissions: ['mappings.view'] },
      { href: '/dashboard/health', label: 'Integration Health', icon: Activity, permissions: ['integration.health.view'] },
    ],
  },
  {
    label: 'Management',
    items: [
      { href: '/dashboard/jobs', label: 'Background Jobs', icon: ListChecks, permissions: ['jobs.manage'] },
      { href: '/dashboard/audit', label: 'Audit Log', icon: ShieldCheck, permissions: ['audit.view'] },
      { href: '/dashboard/notifications', label: 'Notifications', icon: Bell },
      { href: '/dashboard/profile', label: 'Profile', icon: User },
    ],
  },
  {
    label: 'Client-facing',
    items: [
      { href: '/control-panel', label: 'Control Panel', icon: Rocket },
    ],
  },
];

/** Same permission filter for both navs, so mobile can never show what the sidebar hides. */
function useVisibleNav() {
  // Until permissions load, show only the permissionless items — flashing admin
  // links at a technician and then yanking them reads as broken.
  const { data: me } = useQuery({ queryKey: ['me'], queryFn: api.me, staleTime: 5 * 60_000, retry: false });
  const held = new Set(me?.permissions ?? []);
  return (item: NavItem) => !item.permissions || item.permissions.some((p) => held.has(p));
}

/** Compact horizontal nav for below-md, filtered identically to the sidebar. */
export function MobileNav() {
  const pathname = usePathname();
  const can = useVisibleNav();
  return (
    <nav aria-label="Primary" className="flex gap-1 overflow-x-auto border-b border-[var(--border)] bg-[var(--surface)] px-2 py-2 md:hidden">
      {NAV_GROUPS.flatMap((g) => g.items).filter(can).map(({ href, label, icon: Icon }) => (
        <Link
          key={href}
          href={href}
          aria-current={pathname === href ? 'page' : undefined}
          className={`flex shrink-0 items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-medium ${pathname === href
            ? 'bg-brand/10 text-brand'
            : 'text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)]'}`}
        >
          <Icon size={15} />
          {label}
        </Link>
      ))}
    </nav>
  );
}

export function SidebarNav() {
  const pathname = usePathname();
  const can = useVisibleNav();

  return (
    <nav className="flex-1 space-y-5">
      {NAV_GROUPS.map((g, gi) => {
        const visible = g.items.filter(can);
        if (visible.length === 0) return null; // a heading over nothing is clutter
        return (
          <div key={gi} className="space-y-1">
            {g.label && (
              <div className="px-3 pb-1 text-[10px] font-semibold uppercase tracking-wider text-[var(--faint)]">{g.label}</div>
            )}
            {visible.map(({ href, label, icon: Icon }) => {
              const active = pathname === href;
              return (
                <Link
                  key={href}
                  href={href}
                  aria-current={active ? 'page' : undefined}
                  className={`flex items-center gap-3 rounded-lg px-3 py-2 text-sm transition-colors ${active
                    ? 'bg-brand/10 font-medium text-brand'
                    : 'text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)]'}`}
                >
                  <Icon size={18} />
                  {label}
                </Link>
              );
            })}
          </div>
        );
      })}
    </nav>
  );
}

/**
 * Real attachment-storage usage, replacing a hardcoded "68% · 6.8 GB of 10 GB" that was pure
 * decoration. No invented quota: the portal has no storage limit, so showing one manufactured a
 * problem for the user to worry about. Hidden entirely for roles that cannot read the figure.
 */
export function StorageUsage() {
  const { data, isError } = useQuery({ queryKey: ['storage-usage'], queryFn: api.storageUsage, retry: false, staleTime: 60_000 });
  if (isError || !data) return null;

  const fmt = (bytes: number) => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
    return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
  };

  return (
    <div className="mt-4 rounded-xl border border-[var(--border)] bg-[var(--bg)] p-3">
      <div className="flex items-center justify-between text-xs">
        <span className="flex items-center gap-1.5 font-medium"><HardDrive size={13} /> Attachment storage</span>
        <span className="font-semibold">{fmt(data.usedBytes)}</span>
      </div>
      <div className="mt-1.5 text-[11px] text-[var(--muted)]">
        {data.fileCount} file{data.fileCount === 1 ? '' : 's'} across {data.ticketCount} ticket{data.ticketCount === 1 ? '' : 's'}
      </div>
    </div>
  );
}
