import Link from 'next/link';
import { Mail } from 'lucide-react';
import { BrandMark } from '@/components/BrandMark';

/** The one address a visitor can actually reach a human on. */
export const CONTACT_EMAIL = 'proapps@techpio.com';

/**
 * Public-site footer. Deliberately holds only claims that are true of this deployment — no badges,
 * counts, or certifications the product has not earned.
 */
export function MarketingFooter() {
  const year = new Date().getFullYear();
  return (
    <footer className="mt-20 border-t border-[var(--border)] bg-[var(--surface)]">
      <div className="mx-auto grid max-w-6xl gap-10 px-5 py-12 sm:grid-cols-2 lg:grid-cols-4">
        <div className="lg:col-span-2">
          <div className="flex items-center gap-2.5">
            <BrandMark size={34} className="rounded-lg" />
            <span className="text-[15px] font-semibold tracking-tight">Desk Portal</span>
          </div>
          <p className="mt-3 max-w-sm text-sm leading-relaxed text-[var(--muted)]">
            A client ticket portal that sits on top of the PSA you already run. Autotask and
            ConnectWise stay the system of record — nothing is migrated, nothing is replaced.
          </p>
          <p className="mt-4 text-sm text-[var(--muted)]">
            Built by <span className="font-medium text-[var(--fg)]">TechPio</span>
          </p>
        </div>

        <div>
          <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-[var(--faint)]">Product</h2>
          <ul className="space-y-2 text-sm">
            <li><Link href="/" className="text-[var(--muted)] hover:text-[var(--fg)]">Overview</Link></li>
            <li><Link href="/faq" className="text-[var(--muted)] hover:text-[var(--fg)]">FAQ</Link></li>
            <li><Link href="/login" className="text-[var(--muted)] hover:text-[var(--fg)]">Sign in</Link></li>
          </ul>
        </div>

        <div>
          <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-[var(--faint)]">Company</h2>
          <ul className="space-y-2 text-sm">
            <li><Link href="/about" className="text-[var(--muted)] hover:text-[var(--fg)]">About us</Link></li>
            <li><Link href="/contact" className="text-[var(--muted)] hover:text-[var(--fg)]">Contact us</Link></li>
            <li><Link href="/book" className="text-[var(--muted)] hover:text-[var(--fg)]">Book a meeting</Link></li>
            <li>
              <a
                href={`mailto:${CONTACT_EMAIL}`}
                className="inline-flex items-center gap-1.5 text-[var(--muted)] hover:text-[var(--fg)]"
              >
                <Mail size={13} aria-hidden="true" /> {CONTACT_EMAIL}
              </a>
            </li>
          </ul>
        </div>
      </div>

      <div className="border-t border-[var(--border)]">
        <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-3 px-5 py-5 text-xs text-[var(--faint)]">
          <span>© {year} TechPio. All rights reserved.</span>
          <span className="flex flex-wrap items-center gap-x-4 gap-y-2">
            <Link href="/privacy" className="hover:text-[var(--fg)]">Privacy policy</Link>
            <Link href="/terms" className="hover:text-[var(--fg)]">Terms of service</Link>
            <span>Self-hosted · your data stays on your infrastructure</span>
          </span>
        </div>
      </div>
    </footer>
  );
}
