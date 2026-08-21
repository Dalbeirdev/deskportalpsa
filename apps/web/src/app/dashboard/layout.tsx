import { ThemeToggle } from '@/components/ThemeToggle';
import { QueryProvider } from '@/components/QueryProvider';
import { UserMenu } from '@/components/UserMenu';
import { TimerProvider, TimerWidget } from '@/components/TimerProvider';
import { NotificationsBell } from '@/components/NotificationsBell';
import { FileText, Search, HelpCircle, ChevronLeft } from 'lucide-react';
import { SidebarNav, MobileNav, StorageUsage } from '@/components/SidebarNav';
import { BrandMark } from '@/components/BrandMark';
import { UpdateWatchdog } from '@/components/UpdateWatchdog';



export default function DashboardLayout({ children }: { children: React.ReactNode }) {
  return (
    <QueryProvider>
    <TimerProvider>
    <UpdateWatchdog />
    <div className="flex min-h-screen">
      <aside className="hidden w-64 shrink-0 flex-col border-r border-[var(--border)] bg-gradient-to-b from-[var(--surface)] via-[var(--surface)] to-brand/[0.04] p-4 md:flex">
        <div className="mb-6 flex items-center gap-2.5 rounded-xl border border-[var(--border)] bg-[var(--surface)] p-2.5">
          <BrandMark size={36} className="shrink-0 rounded-lg" />
          <div className="min-w-0">
            <div className="truncate text-[15px] font-semibold leading-tight tracking-tight">Desk Portal</div>
            <div className="truncate text-[10px] text-[var(--muted)]">Multi-tenant PSA Portal</div>
          </div>
        </div>
        <SidebarNav />

        <StorageUsage />
        <button className="mt-2 flex items-center gap-2 rounded-lg px-3 py-2 text-xs font-medium text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)]">
          <ChevronLeft size={15} /> Collapse
        </button>
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
            <TimerWidget />
            <NotificationsBell />
            <a href="/user-guide.pdf" target="_blank" rel="noopener noreferrer" className="hidden items-center gap-1.5 rounded-lg px-2.5 py-2 text-sm text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)] sm:inline-flex">
              <HelpCircle size={17} /> Help
            </a>
            <ThemeToggle />
            <UserMenu />
          </div>
        </header>

        {/* Mobile navigation — the sidebar is hidden below md. */}
        <MobileNav />

        <main className="flex-1 bg-[var(--bg)] p-4 sm:p-6">{children}</main>

        <footer className="flex flex-wrap items-center justify-between gap-2 border-t border-[var(--border)] bg-[var(--surface)] px-4 py-3 text-xs text-[var(--muted)] sm:px-6">
          <span>Desk Portal · v0.1.0</span>
          <a href="/user-guide.pdf" target="_blank" rel="noopener noreferrer" className="inline-flex items-center gap-1.5 hover:text-[var(--fg)]">
            <FileText size={13} /> User Guide (PDF)
          </a>
        </footer>
      </div>
    </div>
    </TimerProvider>
    </QueryProvider>
  );
}
