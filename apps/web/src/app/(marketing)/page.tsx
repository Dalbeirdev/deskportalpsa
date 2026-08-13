import Link from 'next/link';
import type { Metadata } from 'next';
import { ArrowRight, RefreshCw, ShieldCheck, Clock, Users, Paperclip, ListChecks } from 'lucide-react';
import { Container, Section, Card, CtaBand } from '@/components/marketing/ui';

export const metadata: Metadata = {
  title: 'Desk Portal — a client ticket portal for Autotask and ConnectWise',
  description:
    'Give your clients a portal on top of the PSA you already run. Tickets, replies, files and time sync both ways. Self-hosted.',
};

const STEPS = [
  {
    title: 'Connect your PSA',
    body: 'Add your Autotask or ConnectWise credentials once. They are stored in a secrets vault, never in the database. Boards, queues, statuses and work types are discovered from your instance.',
  },
  {
    title: 'Map it to your language',
    body: 'Decide how portal statuses and priorities correspond to yours, and which board new tickets land on. Nothing is assumed about how you run your desk.',
  },
  {
    title: 'Invite your people and clients',
    body: 'Technicians sign in with your identity provider and see work by role and board. Clients see only their own company’s tickets.',
  },
];

export default function HomePage() {
  return (
    <>
      <Container className="pt-16 pb-6 sm:pt-24">
        <p className="mb-4 inline-flex items-center gap-2 rounded-full border border-[var(--border)] bg-[var(--surface)] px-3 py-1 text-xs font-medium text-[var(--muted)]">
          <RefreshCw size={12} className="text-brand" aria-hidden="true" />
          Works with Datto Autotask and ConnectWise Manage
        </p>
        <h1 className="max-w-3xl text-4xl font-semibold leading-[1.1] tracking-tight sm:text-5xl">
          A client portal for the PSA you already run.
        </h1>
        <p className="mt-5 max-w-2xl text-lg leading-relaxed text-[var(--muted)]">
          Your clients get somewhere clear to raise and follow tickets. Your technicians keep working
          in Autotask or ConnectWise. Replies, files and time move between the two automatically —
          nothing is migrated, and your PSA stays the system of record.
        </p>
        <div className="mt-8 flex flex-wrap gap-3">
          <Link
            href="/book"
            className="inline-flex items-center gap-2 rounded-lg bg-brand px-5 py-3 text-sm font-medium text-brand-fg hover:opacity-90"
          >
            Book a meeting <ArrowRight size={15} aria-hidden="true" />
          </Link>
          <Link
            href="/contact"
            className="rounded-lg border border-[var(--border)] bg-[var(--surface)] px-5 py-3 text-sm font-medium hover:bg-[var(--bg)]"
          >
            Ask a question
          </Link>
        </div>
      </Container>

      <Section
        title="The gap this closes"
        lead="PSAs are built for technicians. Asking a client to log into one — or to keep emailing the desk and hoping — is where visibility goes missing."
      >
        <div className="grid gap-4 sm:grid-cols-3">
          <Card title="Clients stop chasing">
            They can see every ticket they raised, its current state, and the whole conversation,
            without asking anyone for a status update.
          </Card>
          <Card title="Technicians change nothing">
            No second inbox to watch. Work continues in the PSA, and the portal keeps itself current
            from it.
          </Card>
          <Card title="Nothing is duplicated">
            The PSA remains the single source of truth. The portal reads from it and writes back to
            it — it never becomes a second copy to reconcile.
          </Card>
        </div>
      </Section>

      <Section title="How it works" className="border-y border-[var(--border)] bg-[var(--surface)]">
        <ol className="grid gap-6 sm:grid-cols-3">
          {STEPS.map((s, i) => (
            <li key={s.title}>
              <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-brand text-sm font-semibold text-brand-fg">
                {i + 1}
              </span>
              <h3 className="mt-3 text-[15px] font-semibold">{s.title}</h3>
              <p className="mt-2 text-sm leading-relaxed text-[var(--muted)]">{s.body}</p>
            </li>
          ))}
        </ol>
      </Section>

      <Section
        title="What moves in both directions"
        lead="Sync is two-way and continuous. A note added in the PSA appears in the portal, and a client reply lands on the ticket where your technician is already working."
      >
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <Card title="Conversation">
            Client replies and technician notes stay on one thread. Internal notes remain internal —
            they are never shown to a client.
          </Card>
          <Card title="Files and screenshots">
            Attachments upload from either side and arrive on the other, so a screenshot from a client
            reaches the ticket rather than an inbox.
          </Card>
          <Card title="Time entries">
            Time logged in the portal reaches the PSA with the right work type and role, and time
            logged in the PSA is reflected back.
          </Card>
          <Card title="Status and priority">
            Mapped to your own values, so a portal status means exactly what your board says it means.
          </Card>
          <Card title="Assignment">
            Assign work to the right technician by role and by board, honouring the coverage your PSA
            already defines.
          </Card>
          <Card title="Deletions">
            A note or attachment removed in the PSA is removed in the portal too, so the client is
            never left reading something that was withdrawn.
          </Card>
        </div>
      </Section>

      <Section title="Also included" className="border-y border-[var(--border)] bg-[var(--surface)]">
        <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
          {[
            { icon: Clock, title: 'Time and SLA reporting', body: 'Hours by ticket and technician, SLA compliance, and resolution trends.' },
            { icon: Users, title: 'Roles that mean something', body: 'Administrators, managers, technicians, auditors and client users each see their own slice.' },
            { icon: ListChecks, title: 'Nothing silently lost', body: 'Tickets that failed to reach the PSA are listed and can be pushed again individually.' },
            { icon: Paperclip, title: 'Client control panel', body: 'Clients manage their own contacts, approvers, business hours and holidays.' },
          ].map(({ icon: Icon, title, body }) => (
            <div key={title}>
              <Icon size={20} className="text-brand" aria-hidden="true" />
              <h3 className="mt-3 text-[15px] font-semibold">{title}</h3>
              <p className="mt-2 text-sm leading-relaxed text-[var(--muted)]">{body}</p>
            </div>
          ))}
        </div>
      </Section>

      <Section title="Yours to host">
        <div className="grid gap-4 sm:grid-cols-3">
          <Card title="Runs on your infrastructure">
            The whole stack runs from one Docker host you control. Client data does not leave it.
          </Card>
          <Card title="Your identity provider">
            Sign-in goes through Keycloak, so password policy and multi-factor are governed where
            your other systems already are.
          </Card>
          <Card title="Credentials in a vault">
            PSA API keys are held in a secrets vault, never in the database, and every administrative
            action is written to an append-only audit log.
          </Card>
        </div>
        <p className="mt-6 flex items-center gap-2 text-sm text-[var(--muted)]">
          <ShieldCheck size={16} className="text-brand" aria-hidden="true" />
          Multi-tenant by design — each client company is isolated at the database level, not by a
          filter someone has to remember to write.
        </p>
      </Section>

      <CtaBand
        title="See it against your own PSA."
        lead="Half an hour, your Autotask or ConnectWise instance, and a straight answer about whether this fits how you work."
      />
    </>
  );
}
