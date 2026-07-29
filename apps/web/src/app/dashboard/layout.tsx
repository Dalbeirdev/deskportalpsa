import Link from 'next/link';
import { ThemeToggle } from '@/components/ThemeToggle';
import { QueryProvider } from '@/components/QueryProvider';
import { UserMenu } from '@/components/UserMenu';
import {
  LayoutDashboard, Ticket, Plug, Bell, User, BarChart3, Activity, ListChecks, ShieldCheck,
  SlidersHorizontal, FileText, Search, HelpCircle,
} from 'lucide-react';

const navGroups = [
  {
    label: null,
    items: [
      { href: '/dashboard', label: 'Overview', icon: LayoutDashboard },
      { href: '/dashboard/tickets', label: 'Tickets', icon: Ticket },
      { href: '/dashboard/analytics', label: 'Productivity', icon: BarChart3 },
    ],
  },
  {
    label: 'Integrations',
    items: [
      { href: '/dashboard/connections', label: 'PSA Connections', icon: Plug },
      { href: '/dashboard/mappings', label: 'Field Mapping', icon: SlidersHorizontal },
      { href: '/dashboard/health', label: 'Integration Health', icon: Activity },
    ],
  },
  {
    label: 'Management',
    items: [
      { href: '/dashboard/jobs', label: 'Background Jobs', icon: ListChecks },
      { href: '/dashboard/audit', label: 'Audit Log', icon: ShieldCheck },
      { href: '/dashboard/notifications', label: 'Notifications', icon: Bell },
      { href: '/dashboard/profile', label: 'Profile', icon: User },
    ],
  },
];
const flatNav = navGroups.flatMap((g) => g.items);

export default function DashboardLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-screen">
      <aside className="hidden w-64 shrink-0 border-r border-[var(--border)] bg-[var(--surface)] p-4 md:block">
        <div className="mb-6 flex items-center gap-2.5 px-2">
          <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-brand font-bold text-brand-fg">D</div>
          <div>
            <div className="font-semibold leading-tight">Desk Portal</div>
            <div className="text-[10px] text-[var(--muted)]">Multi-tenant PSA Portal</div>
          </div>
        </div>
        <nav className="space-y-5">
          {navGroups.map((g, gi) => (
            <div key={gi} className="space-y-1">
              {g.label && (
                <div className="px-3 pb-1 text-[10px] font-semibold uppercase tracking-wider text-[var(--faint)]">{g.label}</div>
              )}
              {g.items.map(({ href, label, icon: Icon }) => (
                <Link
                  key={href}
                  href={href}
                  className="flex items-center gap-3 rounded-lg px-3 py-2 text-sm text-[var(--muted)] transition-colors hover:bg-[var(--bg)] hover:text-[var(--fg)]"
                >
                  <Icon size={18} />
                  {label}
                </Link>
              ))}
            </div>
          ))}
        </nav>
      </aside>

      <div className="flex flex-1 flex-col">
        <header className="flex h-14 items-center gap-3 border-b border-[var(--border)] bg-[var(--surface)] px-4 sm:px-6">
          <div className="relative hidden max-w-xl flex-1 md:block">
            <Search size={15} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-[var(--faint)]" />
            <input
              type="search"
              placeholder="Search tickets, customers, technicians… (Ctrl+/)"
              className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] py-2 pl-9 pr-3 text-sm outline-none focus:border-brand"
            />
          </div>
          <div className="ml-auto flex items-center gap-1.5 sm:gap-2">
            <button aria-label="Notifications" className="relative rounded-lg p-2 text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)]">
              <Bell size={18} />
              <span className="absolute right-1 top-1 flex h-4 min-w-4 items-center justify-center rounded-full bg-brand px-1 text-[10px] font-semibold text-brand-fg">4</span>
            </button>
            <a href="/user-guide.pdf" target="_blank" rel="noopener noreferrer" className="hidden items-center gap-1.5 rounded-lg px-2.5 py-2 text-sm text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)] sm:inline-flex">
              <HelpCircle size={17} /> Help
            </a>
            <ThemeToggle />
            <UserMenu />
          </div>
        </header>

        {/* Mobile navigation — the sidebar is hidden below md. */}
        <nav aria-label="Primary" className="flex gap-1 overflow-x-auto border-b border-[var(--border)] bg-[var(--surface)] px-2 py-2 md:hidden">
          {flatNav.map(({ href, label, icon: Icon }) => (
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

        <main className="flex-1 bg-[var(--bg)] p-4 sm:p-6"><QueryProvider>{children}</QueryProvider></main>

        <footer className="flex flex-wrap items-center justify-between gap-2 border-t border-[var(--border)] bg-[var(--surface)] px-4 py-3 text-xs text-[var(--muted)] sm:px-6">
          <span>Desk Portal · v0.1.0</span>
          <a href="/user-guide.pdf" target="_blank" rel="noopener noreferrer" className="inline-flex items-center gap-1.5 hover:text-[var(--fg)]">
            <FileText size={13} /> User Guide (PDF)
          </a>
        </footer>
      </div>
    </div>
  );
}
