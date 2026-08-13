import Link from 'next/link';
import type { Metadata } from 'next';
import {
  ArrowRight, Ticket, RefreshCw, MessageSquare, Paperclip, CircleDot, Flag, UserCheck, Timer,
  Trash2, BarChart3, Contact, CalendarClock, BadgeCheck, Building2, KeyRound, Vault,
  ShieldCheck, Server, Lock, ScrollText, Fingerprint, Boxes, Mail, PhoneOff, Gauge, Eye,
  Wrench, Users, Building, Headphones, Network, Play,
} from 'lucide-react';
import { Shell, Band, SectionHead, FeatureCard, CapabilityTile, Step } from '@/components/marketing/blocks';
import { Reveal } from '@/components/marketing/Reveal';
import { HeroStage } from '@/components/marketing/HeroStage';
import { FlowRail } from '@/components/marketing/FlowRail';
import { Compare } from '@/components/marketing/Compare';
import { ProductTabs } from '@/components/marketing/ProductTabs';
import { SyncLanes, IntegrationCards, IntegrationBadge } from '@/components/marketing/IntegrationSync';
import { BrowserFrame, ProductScreen } from '@/components/marketing/ProductUI';

export const metadata: Metadata = {
  title: 'Desk Portal — the client portal for Autotask and ConnectWise',
  description:
    'Give clients a modern way to raise tickets, reply, share files and track status, while your technicians keep working in Autotask or ConnectWise. Two-way sync, self-hosted, PSA stays the system of record.',
  openGraph: {
    title: 'Desk Portal — the client portal for Autotask and ConnectWise',
    description:
      'A modern client portal built around the PSA you already use. Two-way sync of tickets, replies, files and time.',
    type: 'website',
    siteName: 'Desk Portal',
  },
};

const FEATURES = [
  { icon: Ticket, title: 'Ticket management', body: 'Clients raise requests in a form built for them, not a technician console. Every ticket lands on the board you nominate.' },
  { icon: RefreshCw, title: 'Two-way synchronisation', body: 'Changes flow continuously in both directions, so the portal and the PSA never disagree about a ticket.' },
  { icon: MessageSquare, title: 'Client conversations', body: 'One thread per ticket. Internal notes are filtered on the server and never reach a client.' },
  { icon: Paperclip, title: 'Files and screenshots', body: 'Attachments upload from either side and arrive on the other, so evidence reaches the ticket instead of an inbox.' },
  { icon: CircleDot, title: 'Ticket status', body: 'Portal statuses map to your own values, so a status means exactly what your board says it means.' },
  { icon: Flag, title: 'Priority management', body: 'Priorities are mapped per connection and stay aligned whichever side changes them.' },
  { icon: UserCheck, title: 'Technician assignment', body: 'Assign by role and by board, honouring the coverage your PSA already defines.' },
  { icon: Timer, title: 'Time entries', body: 'Time logged in the portal reaches the PSA with the right work type and role, and time logged in the PSA comes back.' },
  { icon: Trash2, title: 'Deletion reconciliation', body: 'A note or attachment withdrawn in the PSA disappears from the portal too, so clients never read something retracted.' },
  { icon: BarChart3, title: 'SLA and reporting', body: 'Hours by ticket and technician, SLA compliance and resolution trends, drawn from the data your PSA already holds.' },
  { icon: Contact, title: 'Client contacts', body: 'Client administrators manage their own people and who may raise or approve requests.' },
  { icon: CalendarClock, title: 'Business hours and holidays', body: 'Each client keeps its own working calendar, so expectations match the agreement you actually signed.' },
  { icon: BadgeCheck, title: 'Approvals', body: 'Named approvers and escalation levels per client account, configured by the client themselves.' },
  { icon: Building2, title: 'Multi-tenant architecture', body: 'Every client company is isolated at the database level, not by a filter someone has to remember to write.' },
  { icon: KeyRound, title: 'Identity provider and SSO', body: 'Sign-in is delegated to Keycloak, so password policy and multi-factor are governed where your other systems are.' },
  { icon: Vault, title: 'Secure credential vault', body: 'PSA API keys live in a secrets vault, never in the database, and are never shown again after being entered.' },
];

const BENEFITS = [
  { icon: Mail, title: 'Fewer inbound emails', body: 'Requests arrive in one place, in a shape your desk can act on immediately.' },
  { icon: Eye, title: 'Clients can see for themselves', body: 'Status, priority and the latest reply are visible without anyone being asked.' },
  { icon: Wrench, title: 'No workflow change for technicians', body: 'Nobody learns a second system. Work continues in Autotask or ConnectWise.' },
  { icon: ShieldCheck, title: 'Your system of record is protected', body: 'The portal never becomes a second copy of the truth to reconcile.' },
  { icon: PhoneOff, title: 'Less repetitive chasing', body: '“Any update?” is answered by the portal rather than by a technician.' },
  { icon: Gauge, title: 'A support experience clients notice', body: 'A professional portal is a visible difference at renewal time.' },
];

const SECURITY = [
  { icon: Building2, title: 'Multi-tenant isolation', body: 'Enforced on every query at the database level, and covered by tests written to try to break it.' },
  { icon: Fingerprint, title: 'Identity provider integration', body: 'Keycloak handles sign-in, so SSO and multi-factor are configured once, centrally.' },
  { icon: Vault, title: 'Credentials in a vault', body: 'PSA API keys never touch the application database and are never returned by the API.' },
  { icon: ScrollText, title: 'Append-only audit log', body: 'Administrative actions are recorded with the actor and a correlation id, and cannot be edited or deleted.' },
  { icon: Lock, title: 'Role-based access', body: 'Administrators, managers, technicians, auditors and client users each see only their own slice.' },
  { icon: Server, title: 'Your infrastructure', body: 'Self-host the whole stack on a machine you control. Client data need not sit with a vendor.' },
];

const USE_CASES = [
  { icon: Network, title: 'Managed service providers', body: 'Running Autotask or ConnectWise for many client companies at once.' },
  { icon: Headphones, title: 'IT support companies', body: 'Fielding requests from clients who should not need a PSA login.' },
  { icon: Building, title: 'Internal IT teams', body: 'Serving departments or sites that each need their own view.' },
  { icon: Users, title: 'Help desk teams', body: 'Wanting one front door instead of an inbox, a phone and a chat window.' },
];

export default function HomePage() {
  return (
    <>
      <section className="relative isolate overflow-hidden border-b border-[var(--border)]">
        <div aria-hidden="true" className="pointer-events-none absolute inset-0 -z-10">
          <div className="dp-aurora h-[30rem] w-[30rem] bg-brand/20" style={{ top: '-12rem', left: '-8rem' }} />
          <div className="dp-aurora h-[26rem] w-[26rem] bg-brand-soft/35" style={{ top: '-6rem', right: '-6rem', animationDelay: '-6s' }} />
          <div
            className="dp-grid-bg absolute inset-0 opacity-60 dark:opacity-25"
            style={{
              maskImage: 'radial-gradient(ellipse 75% 55% at 50% 0%, #000 40%, transparent 100%)',
              WebkitMaskImage: 'radial-gradient(ellipse 75% 55% at 50% 0%, #000 40%, transparent 100%)',
            }}
          />
        </div>

        <Shell className="grid items-center gap-14 pt-16 pb-20 sm:pt-24 lg:grid-cols-[1fr_1.05fr] lg:gap-16 lg:pb-28">
          <div>
            <div className="dp-rise"><IntegrationBadge /></div>
            <h1
              className="dp-rise mt-5 text-[2.4rem] font-semibold leading-[1.06] tracking-tight sm:text-[3.1rem]"
              style={{ animationDelay: '80ms' }}
            >
              The client portal built around the PSA{' '}
              <span className="text-brand dark:text-brand-soft">you already use.</span>
            </h1>
            <p
              className="dp-rise mt-5 max-w-xl text-[17px] leading-relaxed text-[var(--muted)]"
              style={{ animationDelay: '150ms' }}
            >
              Give clients a simple way to submit tickets, reply, share files and track status —
              while your technicians carry on working inside Autotask or ConnectWise. Nothing is
              migrated, and your PSA stays the system of record.
            </p>
            <div className="dp-rise mt-8 flex flex-wrap gap-3" style={{ animationDelay: '220ms' }}>
              <Link
                href="/book"
                className="inline-flex items-center gap-2 rounded-xl bg-brand px-6 py-3.5 text-sm font-medium text-brand-fg shadow-[0_10px_30px_-12px_rgba(20,83,45,0.8)] transition-transform hover:-translate-y-0.5"
              >
                Book a demo <ArrowRight size={15} aria-hidden="true" />
              </Link>
              <Link
                href="#how-it-works"
                className="inline-flex items-center gap-2 rounded-xl border border-[var(--border)] bg-[var(--surface)] px-6 py-3.5 text-sm font-medium transition-transform hover:-translate-y-0.5"
              >
                <Play size={14} aria-hidden="true" /> See how it works
              </Link>
            </div>
            <p className="dp-rise mt-5 text-[12.5px] text-[var(--faint)]" style={{ animationDelay: '280ms' }}>
              Self-hosted · your data stays on your infrastructure
            </p>
          </div>

          <div className="dp-rise" style={{ animationDelay: '320ms' }}>
            <HeroStage />
          </div>
        </Shell>
      </section>

      <Band>
        <Shell>
          <SectionHead
            eyebrow="The gap"
            title={<>Your PSA works for your technicians. <span className="text-[var(--muted)]">Does it work for your clients?</span></>}
            lead="A PSA is built for the people who resolve tickets. Handing that interface to a client — or leaving them on email — is where visibility disappears."
          />
          <Reveal delay={80} className="mt-10"><Compare /></Reveal>
        </Shell>
      </Band>

      <Band id="how-it-works" tone="raised">
        <Shell>
          <SectionHead
            eyebrow="How it works"
            title="One request. Two experiences. One system of record."
            lead="The client gets a portal. The technician keeps the PSA. Desk Portal keeps the two in step, continuously and in both directions."
            align="center"
          />
          <Reveal delay={80} className="mt-12"><FlowRail /></Reveal>

          <div className="mt-12 grid gap-5 sm:grid-cols-3">
            <Reveal><Step n="01" title="Connect">Add your Autotask or ConnectWise credentials once. Boards, queues, statuses and work types are discovered from your instance.</Step></Reveal>
            <Reveal delay={80}><Step n="02" title="Configure">Map statuses and priorities to your own language, choose default boards, and set who may see and do what.</Step></Reveal>
            <Reveal delay={160}><Step n="03" title="Launch">Invite technicians and clients. They sign in through your identity provider — no separate password to manage.</Step></Reveal>
          </div>
        </Shell>
      </Band>

      <Band id="product">
        <Shell>
          <SectionHead
            eyebrow="The product"
            title="See Desk Portal in action"
            lead="Every screen below is the real interface. Move between them to see what your clients, your technicians and your administrators each work with."
          />
          <Reveal delay={80} className="mt-10"><ProductTabs /></Reveal>
        </Shell>
      </Band>

      <Band tone="raised">
        <Shell className="grid items-center gap-12 lg:grid-cols-2">
          <div>
            <SectionHead
              eyebrow="For your clients"
              title="Make IT support feel simple."
              lead="No PSA login, no training, no wondering where a request went. A client raises it, watches it move, and replies without leaving the page."
            />
            <ol className="mt-8 space-y-4">
              {[
                'Submit a request in a form written for them, not for a technician.',
                'Attach the screenshot that explains it in one drag.',
                'Watch status, priority and assignment change in real time.',
                'Reply in the same thread the technician is already using.',
                'See the time logged against the work, if you choose to show it.',
              ].map((t, i) => (
                <Reveal key={t} delay={i * 60}>
                  <li className="flex gap-3 text-[14.5px] leading-relaxed text-[var(--muted)]">
                    <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-brand text-[11px] font-semibold text-brand-fg">
                      {i + 1}
                    </span>
                    {t}
                  </li>
                </Reveal>
              ))}
            </ol>
          </div>
          <Reveal delay={100}>
            <BrowserFrame url="portal.yourmsp.com/acme"><ProductScreen view="client" /></BrowserFrame>
          </Reveal>
        </Shell>
      </Band>

      <Band>
        <Shell className="grid items-center gap-12 lg:grid-cols-2">
          <Reveal className="order-2 lg:order-1">
            <BrowserFrame url="portal.yourmsp.com/tickets/10482"><ProductScreen view="conversation" /></BrowserFrame>
          </Reveal>
          <div className="order-1 lg:order-2">
            <SectionHead
              eyebrow="For your technicians"
              title="Technicians keep working where they already work."
              lead="Nobody is asked to learn a second system or watch a second queue. Replies written in Autotask or ConnectWise appear in the portal, and client replies land on the ticket already open in front of them."
            />
            <div className="mt-8 grid gap-4 sm:grid-cols-2">
              <FeatureCard icon={RefreshCw} title="No second inbox">The portal is a surface, not a destination. There is nothing extra to check.</FeatureCard>
              <FeatureCard icon={ShieldCheck} title="No risk to the record">Every change is written back to the PSA, which remains the single source of truth.</FeatureCard>
            </div>
          </div>
        </Shell>
      </Band>

      <Band id="integrations" tone="raised">
        <Shell>
          <SectionHead
            eyebrow="Integrations"
            title="Built around the PSA your team already trusts."
            lead="Both connectors run against live instances today, each with its own mappings, defaults and sync settings — an Autotask tenant and a ConnectWise tenant can run side by side."
            align="center"
          />
          <Reveal delay={80} className="mt-10"><IntegrationCards /></Reveal>
          <Reveal delay={140} className="mt-6"><SyncLanes /></Reveal>
        </Shell>
      </Band>

      <Band id="features">
        <Shell>
          <SectionHead
            eyebrow="Capabilities"
            title="What Desk Portal actually does"
            lead="Sixteen things the product does today. Not a roadmap — if something here matters to you, ask us to show it running."
          />
          <div className="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {FEATURES.map((f, i) => (
              <Reveal key={f.title} delay={(i % 4) * 60}>
                <FeatureCard icon={f.icon} title={f.title}>{f.body}</FeatureCard>
              </Reveal>
            ))}
          </div>
        </Shell>
      </Band>

      <Band id="security" tone="ink">
        <Shell>
          <SectionHead
            eyebrow="Security"
            title="Built for MSP environments. Designed for control."
            lead="You are holding other companies' data. The architecture assumes that from the first query rather than adding it later."
            align="center"
            onInk
          />
          <div className="mt-12 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {SECURITY.map(({ icon: Icon, title, body }, i) => (
              <Reveal key={title} delay={(i % 3) * 70}>
                <div className="dp-lift h-full rounded-2xl border border-white/10 bg-white/[0.04] p-5 backdrop-blur">
                  <span className="mb-3 flex h-10 w-10 items-center justify-center rounded-xl bg-brand-soft/15 text-brand-soft">
                    <Icon size={18} aria-hidden="true" />
                  </span>
                  <h3 className="text-[15px] font-semibold text-white">{title}</h3>
                  <p className="mt-2 text-[13.5px] leading-relaxed text-white/60">{body}</p>
                </div>
              </Reveal>
            ))}
          </div>

          <Reveal delay={120} className="mt-12">
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <CapabilityTile value="24/7" label="Client self-service access" />
              <CapabilityTile value="Two-way" label="Continuous PSA sync" />
              <CapabilityTile value="100%" label="PSA remains system of record" />
              <CapabilityTile value="Multi-tenant" label="Isolation at the database level" />
            </div>
            <p className="mt-4 text-center text-[12px] text-white/40">
              Capability statements describing how the product is built — not customer metrics.
            </p>
          </Reveal>
        </Shell>
      </Band>

      <Band>
        <Shell>
          <SectionHead
            eyebrow="Deployment"
            title="Your data. Your infrastructure. Your control."
            lead="The whole stack — portal, API, background worker, database and sign-in — runs from a single Docker host you own."
          />
          <div className="mt-10 grid gap-4 lg:grid-cols-3">
            <Reveal><FeatureCard icon={Server} title="Your infrastructure">One modest virtual server is enough to start, and it can sit alongside sites you already run.</FeatureCard></Reveal>
            <Reveal delay={80}><FeatureCard icon={Fingerprint} title="Your identity">Sign-in goes through your own Keycloak realm, so SSO and MFA follow the policy you already set.</FeatureCard></Reveal>
            <Reveal delay={160}><FeatureCard icon={Boxes} title="Your credentials">PSA keys stay in your vault, on your machine, and never leave it.</FeatureCard></Reveal>
          </div>
        </Shell>
      </Band>

      <Band tone="raised">
        <Shell>
          <SectionHead
            eyebrow="Why MSPs use it"
            title="Built for MSPs. Designed around how your team actually works."
            align="center"
          />
          <div className="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {BENEFITS.map((b, i) => (
              <Reveal key={b.title} delay={(i % 3) * 70}>
                <FeatureCard icon={b.icon} title={b.title}>{b.body}</FeatureCard>
              </Reveal>
            ))}
          </div>
        </Shell>
      </Band>

      <Band>
        <Shell>
          <SectionHead eyebrow="Who it is for" title="Where Desk Portal fits" align="center" />
          <div className="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {USE_CASES.map((u, i) => (
              <Reveal key={u.title} delay={(i % 4) * 60}>
                <FeatureCard icon={u.icon} title={u.title}>{u.body}</FeatureCard>
              </Reveal>
            ))}
          </div>
        </Shell>
      </Band>

      <Shell className="pb-24">
        <Reveal>
          <div className="relative isolate overflow-hidden rounded-3xl bg-brand px-6 py-14 text-brand-fg sm:px-12">
            <div aria-hidden="true" className="pointer-events-none absolute inset-0 -z-10">
              <div className="dp-aurora h-80 w-80 bg-brand-soft/30" style={{ top: '-6rem', right: '-4rem' }} />
              <div className="dp-aurora h-72 w-72 bg-brand-accent/20" style={{ bottom: '-8rem', left: '-3rem', animationDelay: '-5s' }} />
            </div>
            <h2 className="max-w-2xl text-[1.8rem] font-semibold leading-tight tracking-tight sm:text-[2.4rem]">
              Give your clients a better way to work with your IT team.
            </h2>
            <p className="mt-4 max-w-xl text-[16px] leading-relaxed text-brand-fg/75">
              Keep your PSA. Keep your technician workflow. Upgrade the client experience.
            </p>
            <div className="mt-8 flex flex-wrap gap-3">
              <Link
                href="/book"
                className="inline-flex items-center gap-2 rounded-xl bg-brand-fg px-6 py-3.5 text-sm font-medium text-brand transition-transform hover:-translate-y-0.5"
              >
                Book a demo <ArrowRight size={15} aria-hidden="true" />
              </Link>
              <Link
                href="/contact"
                className="rounded-xl border border-brand-fg/30 px-6 py-3.5 text-sm font-medium transition-colors hover:bg-brand-fg/10"
              >
                Talk to us
              </Link>
            </div>
          </div>
        </Reveal>
      </Shell>
    </>
  );
}
