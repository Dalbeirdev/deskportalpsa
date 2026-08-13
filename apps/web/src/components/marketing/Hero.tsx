import { Container } from '@/components/marketing/ui';

/**
 * The banner every public page opens with.
 *
 * The aurora fields are absolutely positioned inside an `overflow-hidden` shell so a blurred blob
 * can never widen the document — a full-bleed decoration that adds a horizontal scrollbar is the
 * classic way this pattern goes wrong on phones.
 *
 * Entrance is staggered CSS rather than an observer: above-the-fold content should never depend on
 * scroll position to become visible.
 */
export function Hero({
  eyebrow,
  title,
  lead,
  actions,
  visual,
  size = 'md',
}: {
  eyebrow?: string;
  title: React.ReactNode;
  lead?: string;
  actions?: React.ReactNode;
  visual?: React.ReactNode;
  size?: 'sm' | 'md' | 'lg';
}) {
  const pad = size === 'lg' ? 'pt-16 pb-20 sm:pt-24 sm:pb-28' : size === 'sm' ? 'pt-12 pb-12' : 'pt-14 pb-16 sm:pt-20 sm:pb-20';

  return (
    <section className="relative isolate overflow-hidden border-b border-[var(--border)]">
      <div aria-hidden="true" className="pointer-events-none absolute inset-0 -z-10">
        <div
          className="dp-aurora h-[26rem] w-[26rem] bg-brand/25 sm:h-[34rem] sm:w-[34rem]"
          style={{ top: '-9rem', left: '-6rem' }}
        />
        <div
          className="dp-aurora h-[22rem] w-[22rem] bg-brand-soft/40 sm:h-[30rem] sm:w-[30rem]"
          style={{ top: '-4rem', right: '-8rem', animationDelay: '-6s', animationDuration: '18s' }}
        />
        <div
          className="dp-aurora h-[18rem] w-[18rem] bg-brand-accent/15"
          style={{ bottom: '-8rem', left: '35%', animationDelay: '-3s', animationDuration: '20s' }}
        />
        {/* Faint grid: gives the colour fields something to sit against so they read as depth. */}
        <div
          className="absolute inset-0 opacity-[0.55] dark:opacity-[0.25]"
          style={{
            backgroundImage:
              'linear-gradient(to right, var(--border) 1px, transparent 1px), linear-gradient(to bottom, var(--border) 1px, transparent 1px)',
            backgroundSize: '56px 56px',
            maskImage: 'radial-gradient(ellipse 80% 60% at 50% 0%, #000 40%, transparent 100%)',
            WebkitMaskImage: 'radial-gradient(ellipse 80% 60% at 50% 0%, #000 40%, transparent 100%)',
          }}
        />
      </div>

      <Container className={pad}>
        <div className={visual ? 'grid items-center gap-12 lg:grid-cols-[1.05fr_1fr]' : ''}>
          <div>
            {eyebrow && (
              <p className="dp-rise mb-4 inline-flex items-center gap-2 rounded-full border border-[var(--border)] bg-[var(--surface)]/80 px-3 py-1 text-xs font-semibold uppercase tracking-widest text-brand backdrop-blur">
                {eyebrow}
              </p>
            )}
            <h1
              className={`dp-rise font-semibold leading-[1.08] tracking-tight ${
                size === 'lg' ? 'text-4xl sm:text-5xl lg:text-[3.4rem]' : 'text-3xl sm:text-4xl'
              }`}
              style={{ animationDelay: '80ms' }}
            >
              {title}
            </h1>
            {lead && (
              <p
                className="dp-rise mt-5 max-w-2xl text-lg leading-relaxed text-[var(--muted)]"
                style={{ animationDelay: '160ms' }}
              >
                {lead}
              </p>
            )}
            {actions && (
              <div className="dp-rise mt-8 flex flex-wrap gap-3" style={{ animationDelay: '240ms' }}>
                {actions}
              </div>
            )}
          </div>

          {visual && (
            <div className="dp-rise" style={{ animationDelay: '320ms' }}>
              {visual}
            </div>
          )}
        </div>
      </Container>
    </section>
  );
}
