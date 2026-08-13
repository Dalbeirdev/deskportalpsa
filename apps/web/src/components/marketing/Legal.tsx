import { Container } from '@/components/marketing/ui';

/**
 * A value only the business can supply — jurisdiction, retention period, registered address.
 *
 * Rendered as a visible slot rather than a plausible-looking default on purpose: a legal document
 * that quietly states an invented governing law is worse than one that visibly has a gap, because
 * nobody ever finds the invented one.
 */
export function Fill({ children }: { children: React.ReactNode }) {
  return (
    <mark className="rounded border border-dashed border-brand-accent bg-brand-accent/10 px-1.5 py-0.5 text-[0.95em] font-medium text-[var(--fg)]">
      {children}
    </mark>
  );
}

export type LegalSection = { id: string; heading: string; body: React.ReactNode };

/**
 * Shared shell for policy documents: a contents list that stays put while you read, and numbered
 * anchored sections so a clause can be linked to directly in an email or a contract.
 */
export function LegalDoc({
  updated,
  intro,
  sections,
}: {
  updated: string;
  intro: React.ReactNode;
  sections: LegalSection[];
}) {
  return (
    <Container className="grid gap-10 pt-12 pb-16 lg:grid-cols-[16rem_1fr]">
      <nav aria-label="On this page" className="lg:sticky lg:top-24 lg:self-start">
        <p className="mb-3 text-xs font-semibold uppercase tracking-widest text-[var(--faint)]">
          On this page
        </p>
        <ol className="space-y-1.5 text-sm">
          {sections.map((s, i) => (
            <li key={s.id}>
              <a
                href={`#${s.id}`}
                className="text-[var(--muted)] transition-colors hover:text-brand"
              >
                <span className="tabular-nums text-[var(--faint)]">{i + 1}.</span> {s.heading}
              </a>
            </li>
          ))}
        </ol>
      </nav>

      <article className="min-w-0">
        <p className="mb-8 text-sm text-[var(--muted)]">Last updated: {updated}</p>

        <div className="mb-10 space-y-4 text-[15px] leading-relaxed text-[var(--muted)]">{intro}</div>

        {sections.map((s, i) => (
          <section key={s.id} id={s.id} className="mb-10 scroll-mt-24">
            <h2 className="mb-3 text-lg font-semibold tracking-tight">
              <span className="mr-2 tabular-nums text-[var(--faint)]">{i + 1}.</span>
              {s.heading}
            </h2>
            <div className="space-y-3 text-[15px] leading-relaxed text-[var(--muted)]">{s.body}</div>
          </section>
        ))}
      </article>
    </Container>
  );
}

/** Bulleted list used throughout both documents. */
export function LegalList({ items }: { items: React.ReactNode[] }) {
  return (
    <ul className="ml-5 list-disc space-y-2">
      {items.map((item, i) => (
        <li key={i}>{item}</li>
      ))}
    </ul>
  );
}
