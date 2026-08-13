import Link from 'next/link';

/** Page-width wrapper. One place decides how wide the public site is. */
export function Container({ children, className = '' }: { children: React.ReactNode; className?: string }) {
  return <div className={`mx-auto w-full max-w-6xl px-5 ${className}`}>{children}</div>;
}

export function PageHeader({ eyebrow, title, lead }: { eyebrow?: string; title: string; lead?: string }) {
  return (
    <Container className="pt-14 pb-8 sm:pt-20">
      {eyebrow && (
        <p className="mb-3 text-xs font-semibold uppercase tracking-widest text-brand">{eyebrow}</p>
      )}
      <h1 className="max-w-3xl text-3xl font-semibold leading-tight tracking-tight sm:text-4xl">{title}</h1>
      {lead && <p className="mt-4 max-w-2xl text-lg leading-relaxed text-[var(--muted)]">{lead}</p>}
    </Container>
  );
}

export function Section({
  title, lead, children, className = '',
}: { title?: string; lead?: string; children: React.ReactNode; className?: string }) {
  return (
    <section className={`py-12 sm:py-16 ${className}`}>
      <Container>
        {title && <h2 className="text-2xl font-semibold tracking-tight sm:text-3xl">{title}</h2>}
        {lead && <p className="mt-3 max-w-2xl text-[var(--muted)]">{lead}</p>}
        <div className={title ? 'mt-8' : ''}>{children}</div>
      </Container>
    </section>
  );
}

export function Card({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5">
      <h3 className="text-[15px] font-semibold">{title}</h3>
      <p className="mt-2 text-sm leading-relaxed text-[var(--muted)]">{children}</p>
    </div>
  );
}

export function CtaBand({ title, lead }: { title: string; lead: string }) {
  return (
    <Container className="pb-4">
      <div className="rounded-2xl bg-brand px-6 py-10 text-brand-fg sm:px-10">
        <h2 className="max-w-2xl text-2xl font-semibold tracking-tight sm:text-3xl">{title}</h2>
        <p className="mt-3 max-w-2xl text-brand-fg/80">{lead}</p>
        <div className="mt-6 flex flex-wrap gap-3">
          <Link
            href="/book"
            className="rounded-lg bg-brand-fg px-4 py-2.5 text-sm font-medium text-brand hover:opacity-90"
          >
            Book a meeting
          </Link>
          <Link
            href="/contact"
            className="rounded-lg border border-brand-fg/35 px-4 py-2.5 text-sm font-medium hover:bg-brand-fg/10"
          >
            Send a message
          </Link>
        </div>
      </div>
    </Container>
  );
}
