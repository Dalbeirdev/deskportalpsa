import Link from 'next/link';
import { Mail, ArrowUpRight, RefreshCw, ServerCog, ShieldCheck } from 'lucide-react';
import { BrandMark } from '@/components/BrandMark';

/** The one address a visitor can actually reach a human on. */
export const CONTACT_EMAIL = 'proapps@techpio.com';

const PRODUCT = [
  { href: '/', label: 'Overview' },
  { href: '/#features', label: 'Features' },
  { href: '/#integrations', label: 'Integrations' },
  { href: '/#security', label: 'Security' },
  { href: '/faq', label: 'FAQ' },
  { href: '/book', label: 'Book a demo' },
];

const COMPANY = [
  { href: '/about', label: 'About' },
  { href: '/contact', label: 'Contact' },
  { href: '/login', label: 'Sign in' },
  { href: '/privacy', label: 'Privacy' },
  { href: '/terms', label: 'Terms' },
];

const INTEGRATIONS = [
  { href: '/#integrations', label: 'Datto Autotask PSA' },
  { href: '/#integrations', label: 'ConnectWise Manage' },
];

/** Claims made here are true of this build. No badges, counts, or certifications it has not earned. */
const PROOF = [
  { icon: RefreshCw, text: 'Two-way sync with Autotask and ConnectWise' },
  { icon: ServerCog, text: 'Self-hosted — runs on your infrastructure' },
  { icon: ShieldCheck, text: 'Credentials in a vault, actions in an audit log' },
];

function FooterLink({ href, label }: { href: string; label: string }) {
  return (
    <li>
      <Link
        href={href}
        className="group inline-flex items-center gap-1 text-sm text-brand-fg/70 transition-colors hover:text-brand-fg"
      >
        {label}
        <ArrowUpRight
          size={13}
          aria-hidden="true"
          className="opacity-0 transition-all group-hover:translate-x-0.5 group-hover:opacity-100"
        />
      </Link>
    </li>
  );
}

/**
 * A brand band, not a sitemap.
 *
 * Sits on the product's own forest with the mark in cream, so the page ends on the brand rather
 * than fading into another sheet of white. The aurora is contained by overflow-hidden — a blurred
 * blob that escapes its box adds a horizontal scrollbar on phones.
 */
export function MarketingFooter() {
  const year = new Date().getFullYear();
  return (
    <footer className="relative isolate mt-20 overflow-hidden bg-brand text-brand-fg">
      <div aria-hidden="true" className="pointer-events-none absolute inset-0 -z-10">
        <div
          className="dp-aurora h-[24rem] w-[24rem] bg-brand-soft/25"
          style={{ top: '-8rem', left: '-4rem' }}
        />
        <div
          className="dp-aurora h-[20rem] w-[20rem] bg-brand-accent/20"
          style={{ bottom: '-9rem', right: '-3rem', animationDelay: '-7s', animationDuration: '19s' }}
        />
        <div
          className="absolute inset-0 opacity-[0.12]"
          style={{
            backgroundImage:
              'linear-gradient(to right, #FDF6E3 1px, transparent 1px), linear-gradient(to bottom, #FDF6E3 1px, transparent 1px)',
            backgroundSize: '56px 56px',
            maskImage: 'radial-gradient(ellipse 70% 70% at 20% 0%, #000 30%, transparent 100%)',
            WebkitMaskImage: 'radial-gradient(ellipse 70% 70% at 20% 0%, #000 30%, transparent 100%)',
          }}
        />
      </div>

      <div className="mx-auto max-w-shell px-5 py-14 sm:px-8">
        <div className="grid gap-10 sm:grid-cols-2 lg:grid-cols-[1.5fr_1fr_1fr_1fr]">
          <div>
            <div className="flex items-center gap-2.5">
              <BrandMark size={38} variant="inverse" className="rounded-lg" />
              <span className="text-base font-semibold tracking-tight">Desk Portal</span>
            </div>

            <p className="mt-4 max-w-sm text-sm leading-relaxed text-brand-fg/70">
              A client ticket portal that sits on top of the PSA you already run. Your technicians
              keep working where they always have.
            </p>

            <ul className="mt-5 space-y-2">
              {PROOF.map(({ icon: Icon, text }) => (
                <li key={text} className="flex items-start gap-2 text-xs text-brand-fg/70">
                  <Icon size={14} aria-hidden="true" className="mt-0.5 shrink-0 text-brand-soft" />
                  {text}
                </li>
              ))}
            </ul>

            <div className="flex flex-wrap gap-2.5">
              <Link
                href="/book"
                className="rounded-lg bg-brand-fg px-4 py-2.5 text-sm font-medium text-brand transition-transform hover:-translate-y-0.5"
              >
                Book a meeting
              </Link>
              <a
                href={`mailto:${CONTACT_EMAIL}`}
                className="inline-flex items-center gap-2 rounded-lg border border-brand-fg/30 px-4 py-2.5 text-sm font-medium transition-colors hover:bg-brand-fg/10"
              >
                <Mail size={14} aria-hidden="true" /> Email us
              </a>
            </div>
          </div>

          <nav aria-label="Product">
            <h2 className="mb-4 text-xs font-semibold uppercase tracking-widest text-brand-soft">
              Product
            </h2>
            <ul className="space-y-2.5">
              {PRODUCT.map((l) => <FooterLink key={l.label} {...l} />)}
            </ul>
          </nav>

          <nav aria-label="Company">
            <h2 className="mb-4 text-xs font-semibold uppercase tracking-widest text-brand-soft">
              Company
            </h2>
            <ul className="space-y-2.5">
              {COMPANY.map((l) => <FooterLink key={l.label} {...l} />)}
            </ul>
          </nav>

          <div>
            <h2 className="mb-4 text-xs font-semibold uppercase tracking-widest text-brand-soft">
              Integrations
            </h2>
            <ul className="space-y-2.5">
              {INTEGRATIONS.map((l) => <FooterLink key={l.label} {...l} />)}
            </ul>
            <h2 className="mb-3 mt-6 text-xs font-semibold uppercase tracking-widest text-brand-soft">
              Contact
            </h2>
            <a
              href={`mailto:${CONTACT_EMAIL}`}
              className="inline-block break-all text-sm text-brand-fg/75 transition-colors hover:text-brand-fg"
            >
              {CONTACT_EMAIL}
            </a>
          </div>
        </div>
      </div>

      <div className="border-t border-brand-fg/15">
        {/* /75, not /60: composited against the forest, 60% opacity lands at 4.1:1 and misses AA. */}
        <div className="mx-auto flex max-w-shell flex-wrap items-center justify-between gap-3 px-5 py-5 text-xs text-brand-fg/75 sm:px-8">
          <span>© {year} TechPIO Services LLP. All rights reserved.</span>
          <span>Desk Portal — built for managed service providers.</span>
        </div>
      </div>
    </footer>
  );
}
