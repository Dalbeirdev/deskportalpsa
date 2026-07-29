import Link from 'next/link';
import { ThemeToggle } from '@/components/ThemeToggle';

/** Login entry. Full Keycloak OIDC (auth-code + PKCE) is wired in the Client Portal phase. */
export default function LoginPage() {
  return (
    <main className="flex min-h-screen items-center justify-center p-6">
      <div className="absolute right-6 top-6">
        <ThemeToggle />
      </div>
      <div className="w-full max-w-sm rounded-2xl border border-[var(--border)] bg-[var(--surface)] p-8 shadow-sm">
        <div className="mb-6 flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-brand text-brand-fg font-bold">D</div>
          <div>
            <h1 className="text-lg font-semibold">Desk Portal</h1>
            <p className="text-sm text-[var(--muted)]">Sign in to continue</p>
          </div>
        </div>

        <form action="/api/auth/login" method="get" className="space-y-4">
          <button
            type="submit"
            className="w-full rounded-lg bg-brand px-4 py-2.5 font-medium text-brand-fg hover:opacity-90 transition-opacity"
          >
            Continue with SSO
          </button>
        </form>

        <p className="mt-6 text-center text-xs text-[var(--muted)]">
          Authentication is provided by your organization&apos;s identity provider.
        </p>
        <p className="mt-2 text-center text-xs text-[var(--muted)]">
          <Link href="/dashboard" className="underline">Preview dashboard shell</Link>
        </p>
      </div>
      <footer className="absolute bottom-4 text-xs text-[var(--muted)]">
        <a href="/user-guide.pdf" target="_blank" rel="noopener noreferrer" className="hover:underline">User Guide (PDF)</a>
      </footer>
    </main>
  );
}
