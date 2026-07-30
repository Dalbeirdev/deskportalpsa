'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import { ChevronDown, LogOut, User, UserCircle2 } from 'lucide-react';

type Session = { authenticated: boolean; name?: string | null; email?: string | null };

const LOCAL_MODE = process.env.NEXT_PUBLIC_LOCAL_MODE === 'true';

/** Signed-in user menu: a real dropdown with profile link and sign-out (sign-out hidden in local
 * demo mode, which has no identity provider to sign out of). */
export function UserMenu() {
  const [session, setSession] = useState<Session | null>(
    LOCAL_MODE ? { authenticated: true, name: 'Demo Admin', email: 'dev-admin@local' } : null,
  );
  const [open, setOpen] = useState(false);

  useEffect(() => {
    if (LOCAL_MODE) return; // demo mode signs in automatically; no Keycloak session to read
    fetch('/api/auth/session')
      .then((r) => r.json())
      .then(setSession)
      .catch(() => setSession({ authenticated: false }));
  }, []);

  if (!session) return <div className="h-8 w-24 animate-pulse rounded-lg bg-[var(--bg)]" />;

  if (!session.authenticated) {
    return (
      <a href="/api/auth/login" className="text-sm font-medium text-brand hover:underline">
        Sign in
      </a>
    );
  }

  return (
    <div className="relative">
      <button onClick={() => setOpen((v) => !v)} onBlur={() => setTimeout(() => setOpen(false), 150)}
        aria-haspopup="menu" aria-expanded={open}
        className="flex items-center gap-1.5 rounded-lg px-2 py-1.5 text-sm text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)]">
        <UserCircle2 size={16} />
        <span className="hidden sm:inline">{session.name ?? session.email ?? 'Signed in'}</span>
        <ChevronDown size={13} className={`transition-transform ${open ? 'rotate-180' : ''}`} />
      </button>

      {open && (
        <div role="menu" className="absolute right-0 z-20 mt-1 w-56 overflow-hidden rounded-lg border border-[var(--border)] bg-[var(--surface)] py-1 text-sm shadow-lg">
          <div className="border-b border-[var(--border)] px-3 py-2">
            <div className="font-medium">{session.name ?? 'Signed in'}</div>
            <div className="truncate text-xs text-[var(--muted)]">{session.email ?? ''}</div>
            {LOCAL_MODE && <div className="mt-0.5 text-[10px] uppercase tracking-wide text-[var(--faint)]">Local demo mode</div>}
          </div>
          <Link href="/dashboard/profile" role="menuitem" className="flex items-center gap-2 px-3 py-2 hover:bg-[var(--bg)]">
            <User size={14} /> Profile
          </Link>
          {!LOCAL_MODE && (
            <a href="/api/auth/logout" role="menuitem" className="flex items-center gap-2 px-3 py-2 hover:bg-[var(--bg)]">
              <LogOut size={14} /> Sign out
            </a>
          )}
        </div>
      )}
    </div>
  );
}
