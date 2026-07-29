import Link from 'next/link';
import { ThemeToggle } from '@/components/ThemeToggle';
import { QueryProvider } from '@/components/QueryProvider';
import { UserMenu } from '@/components/UserMenu';
import { LayoutDashboard, Ticket, Plug, Bell, User, BarChart3, Activity, ListChecks, ShieldCheck, SlidersHorizontal } from 'lucide-react';

const nav = [
  { href: '/dashboard', label: 'Overview', icon: LayoutDashboard },
  { href: '/dashboard/tickets', label: 'Tickets', icon: Ticket },
  { href: '/dashboard/analytics', label: 'Productivity', icon: BarChart3 },
  { href: '/dashboard/notifications', label: 'Notifications', icon: Bell },
  { href: '/dashboard/profile', label: 'Profile', icon: User },
  { href: '/dashboard/connections', label: 'PSA Connections', icon: Plug },
  { href: '/dashboard/mappings', label: 'Field Mapping', icon: SlidersHorizontal },
  { href: '/dashboard/health', label: 'Integration Health', icon: Activity },
  { href: '/dashboard/jobs', label: 'Background Jobs', icon: ListChecks },
  { href: '/dashboard/audit', label: 'Audit Log', icon: ShieldCheck },
];

export default function DashboardLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-screen">
      <aside className="hidden w-64 shrink-0 border-r border-[var(--border)] bg-[var(--surface)] p-4 md:block">
        <div className="mb-8 flex items-center gap-2 px-2">
          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-brand text-brand-fg font-bold">D</div>
          <span className="font-semibold">Desk Portal</span>
        </div>
        <nav className="space-y-1">
          {nav.map(({ href, label, icon: Icon }) => (
            <Link
              key={href}
              href={href}
              className="flex items-center gap-3 rounded-lg px-3 py-2 text-sm text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)] transition-colors"
            >
              <Icon size={18} />
              {label}
            </Link>
          ))}
        </nav>
      </aside>

      <div className="flex flex-1 flex-col">
        <header className="flex h-14 items-center justify-between border-b border-[var(--border)] bg-[var(--surface)] px-4 sm:px-6">
          <span className="hidden text-sm text-[var(--muted)] sm:inline">Multi-tenant PSA ticket portal</span>
          <div className="flex items-center gap-3">
            <UserMenu />
            <ThemeToggle />
          </div>
        </header>

        {/* Mobile navigation — the sidebar is hidden below md, so provide a scrollable strip. */}
        <nav aria-label="Primary" className="flex gap-1 overflow-x-auto border-b border-[var(--border)] bg-[var(--surface)] px-2 py-2 md:hidden">
          {nav.map(({ href, label, icon: Icon }) => (
            <Link
              key={href}
              href={href}
              className="flex shrink-0 items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-medium text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)]"
            >
              <Icon size={15} />
              {label}
            </Link>
          ))}
        </nav>

        <main className="flex-1 p-4 sm:p-6"><QueryProvider>{children}</QueryProvider></main>
      </div>
    </div>
  );
}
