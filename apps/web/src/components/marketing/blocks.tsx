import type { LucideIcon } from 'lucide-react';
import { Reveal } from '@/components/marketing/Reveal';

/** One place decides how wide the site is and how far apart its sections sit. */
export function Shell({ children, className = '' }: { children: React.ReactNode; className?: string }) {
  return <div className={`mx-auto w-full max-w-shell px-5 sm:px-8 ${className}`}>{children}</div>;
}

export function Band({
  children, id, tone = 'plain', className = '',
}: {
  children: React.ReactNode;
  id?: string;
  tone?: 'plain' | 'raised' | 'ink';
  className?: string;
}) {
  const tones = {
    plain: '',
    raised: 'border-y border-[var(--border)] bg-[var(--surface)]',
    ink: 'bg-ink text-white',
  } as const;
  return (
    <section id={id} className={`scroll-mt-20 py-20 sm:py-28 ${tones[tone]} ${className}`}>
      {children}
    </section>
  );
}

export function SectionHead({
  eyebrow, title, lead, align = 'left', onInk = false,
}: {
  eyebrow?: string;
  title: React.ReactNode;
  lead?: string;
  align?: 'left' | 'center';
  onInk?: boolean;
}) {
  return (
    <Reveal className={align === 'center' ? 'mx-auto max-w-2xl text-center' : 'max-w-3xl'}>
      {eyebrow && (
        <p className={`mb-3 text-xs font-semibold uppercase tracking-[0.18em] ${onInk ? 'text-brand-soft' : 'text-brand-mid'}`}>
          {eyebrow}
        </p>
      )}
      <h2 className="text-[1.75rem] font-semibold leading-[1.15] tracking-tight sm:text-[2.25rem]">{title}</h2>
      {lead && (
        <p className={`mt-4 text-[17px] leading-relaxed ${onInk ? 'text-white/65' : 'text-[var(--muted)]'}`}>{lead}</p>
      )}
    </Reveal>
  );
}

export function FeatureCard({
  icon: Icon, title, children, visual,
}: {
  icon: LucideIcon;
  title: string;
  children: React.ReactNode;
  visual?: React.ReactNode;
}) {
  return (
    <div className="dp-lift group flex h-full flex-col rounded-2xl border border-[var(--border)] bg-[var(--surface)] p-5">
      <span className="mb-3 flex h-10 w-10 items-center justify-center rounded-xl bg-brand-tint text-brand-deep transition-colors group-hover:bg-brand group-hover:text-brand-fg dark:bg-brand/25 dark:text-brand-soft">
        <Icon size={18} aria-hidden="true" />
      </span>
      <h3 className="text-[15px] font-semibold">{title}</h3>
      <p className="mt-2 text-[13.5px] leading-relaxed text-[var(--muted)]">{children}</p>
      {visual && <div className="mt-4">{visual}</div>}
    </div>
  );
}

/**
 * Capability statements, not metrics. Nothing here is a customer count or a performance figure,
 * because inventing one would be the fastest way to lose a buyer who checks.
 */
export function CapabilityTile({ value, label }: { value: string; label: string }) {
  return (
    <div className="rounded-2xl border border-white/10 bg-white/[0.04] p-5 text-center backdrop-blur">
      <p className="text-2xl font-semibold tracking-tight text-brand-soft sm:text-3xl">{value}</p>
      <p className="mt-1.5 text-[13px] text-white/60">{label}</p>
    </div>
  );
}

export function Step({ n, title, children }: { n: string; title: string; children: React.ReactNode }) {
  return (
    <div className="relative rounded-2xl border border-[var(--border)] bg-[var(--surface)] p-6">
      <span className="text-[2.5rem] font-semibold leading-none tracking-tight text-brand/15 dark:text-brand-soft/20">
        {n}
      </span>
      <h3 className="mt-2 text-[15px] font-semibold">{title}</h3>
      <p className="mt-2 text-[13.5px] leading-relaxed text-[var(--muted)]">{children}</p>
    </div>
  );
}
