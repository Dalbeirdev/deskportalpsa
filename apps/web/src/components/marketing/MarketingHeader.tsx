'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useState } from 'react';
import { Menu, X } from 'lucide-react';
import { BrandMark } from '@/components/BrandMark';

export const MARKETING_NAV = [
  { href: '/#features', label: 'Features' },
  { href: '/#integrations', label: 'Integrations' },
  { href: '/#security', label: 'Security' },
  { href: '/faq', label: 'FAQ' },
  { href: '/about', label: 'About' },
] as const;

/**
 * Public-site header. Sticky, because the two things a visitor needs — book a call, sign in —
 * should never be a scroll away. The nav collapses to a disclosure below md rather than a drawer:
 * five links do not warrant an overlay.
 */
export function MarketingHeader() {
  const pathname = usePathname();
  const [open, setOpen] = useState(false);

  return (
    <header className="sticky top-0 z-40 border-b border-[var(--border)] bg-[var(--surface)]/75 backdrop-blur-xl">
      <div className="mx-auto flex max-w-shell items-center gap-3 px-5 py-3 sm:px-8">
        <Link href="/" className="flex shrink-0 items-center gap-2.5" onClick={() => setOpen(false)}>
          <BrandMark size={34} className="rounded-lg" />
          <span className="text-[15px] font-semibold tracking-tight">Desk Portal</span>
        </Link>

        <nav className="ml-6 hidden items-center gap-1 md:flex">
          {MARKETING_NAV.map((item) => {
            const active = item.href.startsWith('/#') ? false : pathname === item.href;
            return (
              <Link
                key={item.href}
                href={item.href}
                aria-current={active ? 'page' : undefined}
                className={`rounded-lg px-3 py-2 text-sm transition-colors ${
                  active ? 'font-medium text-brand' : 'text-[var(--muted)] hover:text-[var(--fg)]'
                }`}
              >
                {item.label}
              </Link>
            );
          })}
        </nav>

        <div className="ml-auto flex items-center gap-2">
          <Link
            href="/login"
            className="hidden rounded-lg px-3 py-2 text-sm text-[var(--muted)] hover:text-[var(--fg)] sm:block"
          >
            Sign in
          </Link>
          <Link
            href="/book"
            className="rounded-lg bg-brand px-4 py-2 text-sm font-medium text-brand-fg transition-transform hover:-translate-y-0.5"
          >
            Book a demo
          </Link>
          <button
            type="button"
            onClick={() => setOpen((v) => !v)}
            aria-expanded={open}
            aria-label={open ? 'Close menu' : 'Open menu'}
            className="rounded-lg p-2 text-[var(--muted)] hover:bg-[var(--bg)] md:hidden"
          >
            {open ? <X size={18} /> : <Menu size={18} />}
          </button>
        </div>
      </div>

      {open && (
        <nav className="border-t border-[var(--border)] px-5 py-2 md:hidden">
          {[...MARKETING_NAV, { href: '/login', label: 'Sign in' }].map((item) => (
            <Link
              key={item.href}
              href={item.href}
              onClick={() => setOpen(false)}
              className="block rounded-lg px-3 py-2.5 text-sm text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)]"
            >
              {item.label}
            </Link>
          ))}
        </nav>
      )}
    </header>
  );
}
