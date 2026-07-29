'use client';

import { useEffect, useState } from 'react';
import { LogOut, UserCircle2 } from 'lucide-react';

type Session = { authenticated: boolean; name?: string | null; email?: string | null };

/** Shows the signed-in user and a logout action, or a sign-in link when there's no session. */
const LOCAL_MODE = process.env.NEXT_PUBLIC_LOCAL_MODE === 'true';

export function UserMenu() {
  const [session, setSession] = useState<Session | null>(LOCAL_MODE ? { authenticated: true, name: 'Demo Admin' } : null);

  useEffect(() => {
    if (LOCAL_MODE) return; // demo mode signs in automatically; no Keycloak session to read
    fetch('/api/auth/session')
      .then((r) => r.json())
      .then(setSession)
      .catch(() => setSession({ authenticated: false }));
  }, []);

  if (!session) return <div className="h-8 w-24 animate-pulse rounded-lg bg-[var(--bg)]" />;

  if (LOCAL_MODE) {
    return (
      <span className="flex items-center gap-1.5 text-sm text-[var(--muted)]">
        <UserCircle2 size={16} /> Demo Admin
      </span>
    );
  }

  if (!session.authenticated) {
    return (
      <a href="/api/auth/login" className="text-sm font-medium text-brand hover:underline">
        Sign in
      </a>
    );
  }

  return (
    <div className="flex items-center gap-3">
      <span className="hidden items-center gap-1.5 text-sm text-[var(--muted)] sm:flex">
        <UserCircle2 size={16} />
        {session.name ?? session.email ?? 'Signed in'}
      </span>
      <a
        href="/api/auth/logout"
        className="inline-flex items-center gap-1.5 rounded-lg border border-[var(--border)] px-2.5 py-1.5 text-xs font-medium text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)]"
      >
        <LogOut size={14} /> Sign out
      </a>
    </div>
  );
}
