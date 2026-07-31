'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { QueryProvider } from '@/components/QueryProvider';
import { ThemeToggle } from '@/components/ThemeToggle';
import { UserMenu } from '@/components/UserMenu';
import { api } from '@/lib/api';
import {
  Rocket, FileText, Users, Server, UserCheck, ArrowUpCircle, Clock, CalendarDays,
  Megaphone, BarChart3, LayoutDashboard, Lock,
} from 'lucide-react';

type Section = {
  key: string; href: string; label: string; icon: React.ElementType; live: boolean;
};

// Sections mirror the client control panel; keys match the API's ControlPanelSection camelCase names.
// CP-1 ships Ticket Instructions + Users as live; the rest are placeholders for CP-2 / CP-3.
const MANAGEMENT: Section[] = [
  { key: 'ticketInstructions', href: '/control-panel/instructions', label: 'Ticket Instructions', icon: FileText, live: true },
  { key: 'users', href: '/control-panel/users', label: 'Users', icon: Users, live: true },
  { key: 'approvers', href: '#', label: 'Approvers', icon: UserCheck, live: false },
  { key: 'escalation', href: '#', label: 'Escalation Procedures', icon: ArrowUpCircle, live: false },
  { key: 'businessHours', href: '#', label: 'Business Hours', icon: Clock, live: false },
  { key: 'holidays', href: '#', label: 'Holidays', icon: CalendarDays, live: false },
  { key: 'announcements', href: '#', label: 'Announcements', icon: Megaphone, live: false },
];
const ACCOUNTS: Section[] = [
  { key: 'accounts', href: '#', label: 'Accounts & Devices', icon: Server, live: false },
  { key: 'reports', href: '#', label: 'Reports', icon: BarChart3, live: false },
];

export default function ControlPanelLayout({ children }: { children: React.ReactNode }) {
  return (
    <QueryProvider>
      <Shell>{children}</Shell>
    </QueryProvider>
  );
}

function Shell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const { data: caps } = useQuery({ queryKey: ['cp-capabilities'], queryFn: api.cpCapabilities, retry: false });
  const allowed = new Set(caps?.sections ?? []);

  const renderGroup = (label: string, items: Section[]) => {
    // A section is shown when the caller can access it (admins get every key). Until capabilities
    // load we optimistically show the two live CP-1 sections so the panel isn't blank.
    const visible = items.filter((s) => (caps ? allowed.has(s.key) : s.live));
    if (visible.length === 0) return null;
    return (
      <div className="space-y-1">
        <div className="px-3 pb-1 text-[10px] font-semibold uppercase tracking-wider text-[var(--faint)]">{label}</div>
        {visible.map((s) => {
          const active = s.live && pathname.startsWith(s.href);
          const cls = `flex items-center justify-between gap-3 rounded-lg px-3 py-2 text-sm transition-colors ${
            active ? 'bg-brand text-brand-fg' : 'text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)]'
          } ${s.live ? '' : 'cursor-default opacity-60'}`;
          const inner = (
            <>
              <span className="flex items-center gap-3"><s.icon size={18} /> {s.label}</span>
              {!s.live && <span className="rounded-full bg-[var(--bg)] px-1.5 py-0.5 text-[9px] font-semibold uppercase tracking-wide text-[var(--faint)]">Soon</span>}
            </>
          );
          return s.live
            ? <Link key={s.key} href={s.href} className={cls}>{inner}</Link>
            : <div key={s.key} className={cls} aria-disabled title="Coming soon">{inner}</div>;
        })}
      </div>
    );
  };

  return (
    <div className="flex min-h-screen">
      <aside className="hidden w-64 shrink-0 flex-col border-r border-[var(--border)] bg-[var(--surface)] p-4 md:flex">
        <div className="mb-6 flex items-center gap-2.5 px-2">
          <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-brand text-brand-fg"><Rocket size={18} /></div>
          <div>
            <div className="font-semibold leading-tight">Control Panel</div>
            <div className="text-[10px] text-[var(--muted)]">{caps?.companyName ?? 'Client portal'}</div>
          </div>
        </div>
        <nav className="flex-1 space-y-5">
          {renderGroup('Management', MANAGEMENT)}
          {renderGroup('Accounts', ACCOUNTS)}
        </nav>
        <Link href="/dashboard" className="mt-2 flex items-center gap-2 rounded-lg px-3 py-2 text-xs font-medium text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)]">
          <LayoutDashboard size={15} /> Admin Dashboard
        </Link>
      </aside>

      <div className="flex flex-1 flex-col">
        <header className="flex h-14 items-center gap-3 border-b border-[var(--border)] bg-[var(--surface)] px-4 sm:px-6">
          <div className="flex items-center gap-2 font-semibold md:hidden"><Rocket size={17} className="text-brand" /> Control Panel</div>
          {caps && (
            <span className="hidden items-center gap-1.5 rounded-full bg-[var(--bg)] px-2.5 py-1 text-xs text-[var(--muted)] md:inline-flex">
              {caps.isCompanyAdministrator
                ? <><Lock size={11} /> Company Administrator</>
                : <>Client User</>}
            </span>
          )}
          <div className="ml-auto flex items-center gap-1.5 sm:gap-2">
            <ThemeToggle />
            <UserMenu />
          </div>
        </header>

        {/* Mobile nav */}
        <nav aria-label="Primary" className="flex gap-1 overflow-x-auto border-b border-[var(--border)] bg-[var(--surface)] px-2 py-2 md:hidden">
          {[...MANAGEMENT, ...ACCOUNTS].filter((s) => s.live && (!caps || allowed.has(s.key))).map((s) => (
            <Link key={s.key} href={s.href} className="flex shrink-0 items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-medium text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)]">
              <s.icon size={15} /> {s.label}
            </Link>
          ))}
        </nav>

        <main className="flex-1 bg-[var(--bg)] p-4 sm:p-6">{children}</main>
      </div>
    </div>
  );
}
