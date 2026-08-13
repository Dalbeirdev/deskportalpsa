import type { Metadata } from 'next';
import { PageHeader, Section, Card, CtaBand, Container } from '@/components/marketing/ui';

export const metadata: Metadata = {
  title: 'About us — Desk Portal',
  description:
    'Desk Portal is built by TechPio for managed service providers who already run Autotask or ConnectWise.',
};

export default function AboutPage() {
  return (
    <>
      <PageHeader
        eyebrow="About us"
        title="Built by people who work in a PSA every day."
        lead="Desk Portal is made by TechPio. It exists because the gap between a managed service provider and its clients is usually filled with email, and email loses things."
      />

      <Section title="Why we built it">
        <div className="max-w-3xl space-y-4 text-[15px] leading-relaxed text-[var(--muted)]">
          <p>
            Every managed service provider already has a system of record. It holds the boards, the
            SLAs, the time entries and the history. What it rarely has is a front door a client can
            comfortably walk through.
          </p>
          <p>
            The usual answers are unsatisfying. Give clients a PSA login and they meet an interface
            built for technicians. Leave them on email and nobody can answer &ldquo;what is happening
            with my ticket?&rdquo; without going and looking. Move to a product that replaces the PSA
            and you have thrown away years of history and process.
          </p>
          <p>
            So Desk Portal takes the opposite position: your PSA stays exactly as it is and remains
            the source of truth. The portal is a clear, fast surface on top of it for the people who
            are not technicians — and it keeps itself honest by syncing continuously in both
            directions.
          </p>
        </div>
      </Section>

      <Section title="How we approach it" className="border-y border-[var(--border)] bg-[var(--surface)]">
        <div className="grid gap-4 sm:grid-cols-3">
          <Card title="Your PSA is not the enemy">
            We are not trying to replace Autotask or ConnectWise, or to become a second place your
            team has to check. Whatever the PSA says, wins.
          </Card>
          <Card title="Self-hosted by default">
            You run it, on infrastructure you control, with your own identity provider. Your clients&apos;
            data does not need to sit with a vendor for the product to work.
          </Card>
          <Card title="Honest about limits">
            Providers differ. Where one supports something the other does not, the portal degrades
            gracefully and says so, rather than pretending parity and failing quietly.
          </Card>
        </div>
      </Section>

      <Section title="Who it is for">
        <div className="grid gap-4 sm:grid-cols-2">
          <Card title="Managed service providers">
            Running Autotask or ConnectWise, with clients who keep asking for status updates, and a
            desk that would rather be fixing things than reporting on them.
          </Card>
          <Card title="Internal IT teams">
            Running a PSA for an organisation with several sites or departments, who need each one to
            see its own tickets without seeing everyone else&apos;s.
          </Card>
        </div>
      </Section>

      <Container className="pb-10">
        <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-6">
          <h2 className="text-[15px] font-semibold">A note on where the product is</h2>
          <p className="mt-2 max-w-3xl text-sm leading-relaxed text-[var(--muted)]">
            Desk Portal is actively being built and deployed. Both the Autotask and ConnectWise
            connectors are working against live instances, and two-way sync of conversation,
            attachments and time is in place. We would rather tell you plainly what it does today
            than describe a roadmap as though it had already shipped — so if you have a specific
            requirement, ask, and we will tell you whether it exists yet.
          </p>
        </div>
      </Container>

      <CtaBand
        title="Talk to the people who built it."
        lead="No sales team to get past — you will be speaking to someone who knows how the sync actually works."
      />
    </>
  );
}
