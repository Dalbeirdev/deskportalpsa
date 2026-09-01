'use client';

import { useEffect, useState } from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { SidebarNav, StorageUsage } from '@/components/SidebarNav';
import { BrandMark } from '@/components/BrandMark';

/**
 * The desk sidebar, collapsed to icons by default.
 *
 * A technician works inside a ticket, not inside the navigation — and with the ticket page now
 * carrying its own properties rail, a permanently expanded 256px nav spent a fifth of the screen
 * repeating where you already are. Collapsed it is 68px: every destination still one click away,
 * just without the labels.
 *
 * The old "Collapse" button had no handler at all — a control that looked like a feature and did
 * nothing. This replaces it with one that works, remembers the choice, and can be driven from the
 * keyboard.
 */
const KEY = 'desk.sidebar.collapsed';

export function SidebarShell() {
  // Default collapsed. Read the stored preference AFTER mount: reading localStorage during render
  // makes the server and client disagree about the first paint, which React reports as a hydration
  // mismatch and resolves by throwing away the markup.
  const [collapsed, setCollapsed] = useState(true);

  useEffect(() => {
    try {
      const saved = window.localStorage.getItem(KEY);
      if (saved !== null) setCollapsed(saved === '1');
    } catch {
      // Private windows and blocked site data throw on access; the default stands.
    }
  }, []);

  function toggle() {
    setCollapsed((prev) => {
      const next = !prev;
      try { window.localStorage.setItem(KEY, next ? '1' : '0'); } catch { /* preference is a nicety */ }
      return next;
    });
  }

  return (
    <aside
      data-collapsed={collapsed ? 'true' : undefined}
      className={`hidden shrink-0 flex-col border-r border-[var(--border)] bg-gradient-to-b from-[var(--surface)] via-[var(--surface)] to-brand/[0.04] transition-[width] duration-200 md:flex ${
        collapsed ? 'w-[68px] px-2 py-4' : 'w-64 p-4'
      }`}
    >
      <div
        className={`mb-6 flex items-center rounded-xl border border-[var(--border)] bg-[var(--surface)] ${
          collapsed ? 'justify-center p-1.5' : 'gap-2.5 p-2.5'
        }`}
        title={collapsed ? 'Desk Portal' : undefined}
      >
        <BrandMark size={collapsed ? 32 : 36} className="shrink-0 rounded-lg" />
        {!collapsed && (
          <div className="min-w-0">
            <div className="truncate text-[15px] font-semibold leading-tight tracking-tight">Desk Portal</div>
            <div className="truncate text-[10px] text-[var(--muted)]">Multi-tenant PSA Portal</div>
          </div>
        )}
      </div>

      <SidebarNav collapsed={collapsed} />

      {/* Storage is a figure, not an icon — there is nothing meaningful to show at 68px. */}
      {!collapsed && <StorageUsage />}

      <button
        type="button"
        onClick={toggle}
        aria-expanded={!collapsed}
        aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
        title={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
        className={`mt-2 flex items-center rounded-lg py-2 text-xs font-medium text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)] ${
          collapsed ? 'justify-center px-2' : 'gap-2 px-3'
        }`}
      >
        {collapsed ? <ChevronRight size={15} /> : <><ChevronLeft size={15} /> Collapse</>}
      </button>
    </aside>
  );
}
