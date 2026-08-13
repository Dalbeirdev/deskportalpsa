import Link from 'next/link';
import { ThemeToggle } from '@/components/ThemeToggle';
import { BrandMark } from '@/components/BrandMark';

/** Login entry. Uses Keycloak OIDC (auth-code + PKCE); in local demo mode it skips straight in. */
export default function LoginPage() {
  const localMode = process.env.NEXT_PUBLIC_LOCAL_MODE === 'true';
  return (
    <main className="flex min-h-screen items-center justify-center p-6">
      <div className="absolute right-6 top-6">
        <ThemeToggle />
      </div>
      <div className="w-full max-w-sm rounded-2xl border border-[var(--border)] bg-[var(--surface)] p-8 shadow-sm">
        <div className="mb-6 flex items-center gap-3">
          <BrandMark size={40} className="shrink-0 rounded-xl" />
          <div>
            <h1 className="text-lg font-semibold">Desk Portal</h1>
            <p className="text-sm text-[var(--muted)]">{localMode ? 'Local demo mode' : 'Sign in to continue'}</p>
          </div>
        </div>

        {localMode ? (
          <Link
            href="/dashboard"
            className="block w-full rounded-lg bg-brand px-4 py-2.5 text-center font-medium text-brand-fg transition-opacity hover:opacity-90"
          >
            Enter portal
          </Link>
        ) : (
          <form action="/api/auth/login" method="get" className="space-y-4">
            <button
              type="submit"
              className="w-full rounded-lg bg-brand px-4 py-2.5 font-medium text-brand-fg transition-opacity hover:opacity-90"
            >
              Continue with SSO
            </button>
          </form>
        )}

        <p className="mt-6 text-center text-xs text-[var(--muted)]">
          {localMode
            ? 'Demo mode signs you in automatically — no identity provider needed.'
            : "Authentication is provided by your organization's identity provider."}
        </p>
        {!localMode && (
          <p className="mt-2 text-center text-xs text-[var(--muted)]">
            <Link href="/dashboard" className="underline">Preview dashboard shell</Link>
          </p>
        )}
      </div>
      <footer className="absolute bottom-4 text-xs text-[var(--muted)]">
        <a href="/user-guide.pdf" target="_blank" rel="noopener noreferrer" className="hover:underline">User Guide (PDF)</a>
      </footer>
    </main>
  );
}
