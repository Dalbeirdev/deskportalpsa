import Link from 'next/link';
import type { Metadata } from 'next';
import {
  ArrowRight, Ticket, RefreshCw, MessageSquare, Paperclip, CircleDot, Bell, Wrench, Boxes,
  ShieldCheck, Server, Lock, ScrollText, Fingerprint, Building2, Mail, PhoneOff, Gauge, Eye,
  Users, Building, Headphones, Network, Play, ArrowDown,
} from 'lucide-react';
import { Shell, Band, SectionHead, FeatureCard, CapabilityTile, Step } from '@/components/marketing/blocks';
import { Reveal } from '@/components/marketing/Reveal';
import { HeroStage } from '@/components/marketing/HeroStage';
import { FlowRail } from '@/components/marketing/FlowRail';
import { Compare } from '@/components/marketing/Compare';
import { ProductTabs } from '@/components/marketing/ProductTabs';
import { SyncLanes, IntegrationBadge } from '@/components/marketing/IntegrationSync';
import { PsaEcosystem, PsaGrid } from '@/components/marketing/PsaEcosystem';
import { BrowserFrame, ProductScreen } from '@/components/marketing/ProductUI';
import { PSA_PLATFORMS } from '@/lib/psaPlatforms';

export const metadata: Metadata = {
  title: 'Desk Portal — one modern client portal for the PSA your MSP already runs',
  description:
    'Give clients a modern way to submit requests, follow updates and share files, while your technicians keep working in the PSA they already use. Multi-PSA by design.',
  openGraph: {
    title: 'Desk Portal — one modern client portal. Any PSA.',
    description:
      'A client experience platform that sits on top of your existing PSA. Two-way sync, multi-tenant, self-hosted.',
    type: 'website',
    siteName: 'Desk Portal',
  },
};

const FEATURES = [
  { icon: Ticket, title: 'Client ticketing', body: 'A simple way for clients to submit and track support requests, in language written for them.' },
  { icon: RefreshCw, title: 'Two-way sync', body: 'Client communication stays in step with your PSA, continuously and in both directions.' },
  { icon: MessageSquare, title: 'Conversations', body: 'Support communication stays attached to the request. Internal notes remain internal.' },
  { icon: Paperclip, title: 'File sharing', body: 'Clients share screenshots and documents that land on the request, not in an inbox.' },
  { icon: CircleDot, title: 'Status visibility', body: 'Clear progress on every request, so nobody has to ask where something stands.' },
  { icon: Bell, title: 'Notifications', body: 'Clients stay informed without a chain of forwarded emails.' },
  { icon: Wrench, title: 'Technician workflow', body: 'Technicians carry on in the PSA they already use. Nothing new to learn or watch.' },
  { icon: Boxes, title: 'Multi-PSA architecture', body: 'One client experience across different PSA platforms, built to add more.' },
];

const BENEFITS = [
  { icon: Mail, title: 'Fewer inbound emails', body: 'Requests arrive in one place, in a shape your desk can act on immediately.' },
  { icon: Eye, title: 'Clients can see for themselves', body: 'Progress and responses are visible without anyone being asked.' },
  { icon: Wrench, title: 'No workflow change', body: 'Your team keeps its PSA, its boards and its habits.' },
  { icon: ShieldCheck, title: 'Your system of record is safe', body: 'The portal never becomes a second version of the truth.' },
  { icon: PhoneOff, title: 'Less chasing', body: '“Any update?” is answered by the portal, not by a technician.' },
  { icon: Gauge, title: 'An experience clients notice', body: 'A professional support portal is a visible difference at renewal.' },
];

const SECURITY = [
  { icon: Building2, title: 'Multi-tenant architecture', body: 'Each client company is isolated from every other, enforced on every request.' },
  { icon: Lock, title: 'Role-based access', body: 'Administrators, managers, technicians and client users each see only their own view.' },
  { icon: Fingerprint, title: 'SSO and MFA', body: 'Sign-in runs through your identity provider, so existing policy applies unchanged.' },
  { icon: ScrollText, title: 'Audit logging', body: 'Administrative activity is recorded and cannot be quietly altered afterwards.' },
  { icon: ShieldCheck, title: 'Secure credentials', body: 'PSA credentials are held in a secrets vault, never in the application database.' },
  { icon: Server, title: 'Deploy where you choose', body: 'Self-host the platform on infrastructure you control.' },
];

const USE_CASES = [
  { icon: Network, title: 'MSPs', body: 'Deliver a professional client experience without changing your PSA.' },
  { icon: Building, title: 'Growing IT service providers', body: 'Standardise client communication across every customer you serve.' },
  { icon: Boxes, title: 'Multi-PSA MSPs', body: 'One consistent client experience across different PSA environments.' },
  { icon: Headphones, title: 'IT support teams', body: 'Cut repetitive status requests and the chasing that comes with them.' },
  { icon: Users, title: 'Help desk teams', body: 'One front door instead of an inbox, a phone line and a chat window.' },
  { icon: Wrench, title: 'Service delivery leads', body: 'Keep technicians focused on resolving work rather than reporting on it.' },
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
              The client portal that works with{' '}
              <span className="text-brand dark:text-brand-soft">your PSA.</span>
            </h1>
            <p
              className="dp-rise mt-5 max-w-xl text-[17px] leading-relaxed text-[var(--muted)]"
              style={{ animationDelay: '150ms' }}
            >
              Give clients a modern way to submit requests, follow updates, share files and talk to
              your support team — while your technicians continue working in the PSA they already
              use. You should not have to replace your PSA to improve the client experience.
            </p>
            <div className="dp-rise mt-8 flex flex-wrap gap-3" style={{ animationDelay: '220ms' }}>
              <Link
                href="/book"
                className="inline-flex items-center gap-2 rounded-xl bg-brand px-6 py-3.5 text-sm font-medium text-brand-fg shadow-[0_10px_30px_-12px_rgba(20,83,45,0.8)] transition-transform hover:-translate-y-0.5"
              >
                Book a demo <ArrowRight size={15} aria-hidden="true" />
              </Link>
              <Link
                href="#integrations"
                className="inline-flex items-center gap-2 rounded-xl border border-[var(--border)] bg-[var(--surface)] px-6 py-3.5 text-sm font-medium transition-transform hover:-translate-y-0.5"
              >
                <Play size={14} aria-hidden="true" /> Explore integrations
              </Link>
            </div>
            <p className="dp-rise mt-5 text-[12.5px] text-[var(--faint)]" style={{ animationDelay: '280ms' }}>
              {PSA_PLATFORMS.length} PSA platforms · self-hosted · your PSA stays the system of record
            </p>
          </div>

          <div className="dp-rise" style={{ animationDelay: '320ms' }}>
            <HeroStage />
          </div>
        </Shell>
      </section>

      <Band id="integrations" tone="raised">
        <Shell>
          <SectionHead
            eyebrow="Integrations"
            title="One portal. Multiple PSA platforms."
            lead="Connect your PSA environment to Desk Portal and give clients a consistent experience across your service desk ecosystem."
            align="center"
          />
          <Reveal delay={80} className="mt-14"><PsaEcosystem /></Reveal>
          <Reveal delay={120} className="mt-12 hidden lg:block"><PsaGrid /></Reveal>
        </Shell>
      </Band>

      <Band>
        <Shell>
          <SectionHead
            eyebrow="The gap"
            title={<>Your PSA works for your technicians. <span className="text-[var(--muted)]">Does it work for your clients?</span></>}
            lead="A PSA is built for the people who resolve work. Handing that interface to a client — or leaving them on email — is where visibility disappears."
          />
          <Reveal delay={80} className="mt-10"><Compare /></Reveal>
        </Shell>
      </Band>

      <Band id="how-it-works" tone="raised">
        <Shell>
          <SectionHead
            eyebrow="How it works"
            title="One request. Two experiences. One system of record."
            lead="The client gets a portal. The technician keeps the PSA. Desk Portal keeps the two in step."
            align="center"
          />
          <Reveal delay={80} className="mt-12"><FlowRail /></Reveal>

          <div className="mt-12 grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
            <Reveal><Step n="01" title="Choose your PSA">Connect Desk Portal to the PSA your MSP already runs.</Step></Reveal>
            <Reveal delay={70}><Step n="02" title="Configure the experience">Decide what clients see, how statuses read, and who may do what.</Step></Reveal>
            <Reveal delay={140}><Step n="03" title="Invite your clients">Give each client access to their own support experience.</Step></Reveal>
            <Reveal delay={210}><Step n="04" title="Start working">Clients use the portal. Technicians carry on in the PSA.</Step></Reveal>
          </div>
        </Shell>
      </Band>

      <Band id="product">
        <Shell>
          <SectionHead
            eyebrow="The product"
            title="See Desk Portal in action"
            lead="A look at the client experience: requests, conversation, shared files, progress, and the sync that keeps your PSA current."
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
              lead="No PSA login, no training, no wondering where a request went."
            />
            <ol className="mt-8 space-y-4">
              {[
                'Submit a request in a form written for them, not for a technician.',
                'Attach the screenshot that explains it in one drag.',
                'Follow progress without asking anyone for an update.',
                'Reply in the same thread your team is already using.',
                'Get told when something changes, without an email chain.',
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
            <BrowserFrame><ProductScreen view="requests" /></BrowserFrame>
          </Reveal>
        </Shell>
      </Band>

      <Band>
        <Shell className="grid items-center gap-12 lg:grid-cols-2">
          <Reveal className="order-2 lg:order-1">
            <BrowserFrame url="Connected PSA"><ProductScreen view="sync" /></BrowserFrame>
          </Reveal>
          <div className="order-1 lg:order-2">
            <SectionHead
              eyebrow="For your technicians"
              title="Technicians keep working where they already work."
              lead="Nobody learns a second system or watches a second queue. Replies written in your PSA reach the client, and client replies land on the request already in front of your team."
            />
            <div className="mt-8 grid gap-4 sm:grid-cols-2">
              <FeatureCard icon={RefreshCw} title="No second inbox">The portal is a surface, not a destination. There is nothing extra to check.</FeatureCard>
              <FeatureCard icon={ShieldCheck} title="No risk to the record">Every change is written back, so the PSA stays the single source of truth.</FeatureCard>
            </div>
          </div>
        </Shell>
      </Band>

      <Band tone="raised">
        <Shell>
          <SectionHead
            eyebrow="Two-way sync"
            title="Everything stays in step, both directions."
            lead="Whichever PSA you connect, the same things move — and they move continuously rather than on demand."
            align="center"
          />
          <Reveal delay={80} className="mt-10"><SyncLanes /></Reveal>
        </Shell>
      </Band>

      <Band id="features">
        <Shell>
          <SectionHead
            eyebrow="Features"
            title="Everything your clients need. Nothing your technicians need to relearn."
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
            title="Enterprise-grade control without enterprise-level complexity."
            lead="You are holding other companies' data. The platform is built that way from the first request rather than hardened later."
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
              <CapabilityTile value="Multi-PSA" label="One experience, many platforms" />
              <CapabilityTile value="Multi-tenant" label="Client isolation by design" />
            </div>
            <p className="mt-4 text-center text-[12px] text-white/40">
              Capability statements describing how the platform is built — not customer metrics.
            </p>
          </Reveal>
        </Shell>
      </Band>

      <Band>
        <Shell>
          <SectionHead
            eyebrow="Built to grow"
            title="More PSAs. One client experience."
            lead="Whether your MSP runs ConnectWise, Autotask, HaloPSA, Syncro, SuperOps or another supported platform, your clients get the same experience — and your team keeps its own."
          />
          <div className="mt-10 grid gap-4 lg:grid-cols-3">
            <Reveal><FeatureCard icon={Boxes} title="A shared connector layer">Every platform reuses the same sync, mapping and portal, so a new one behaves like the last.</FeatureCard></Reveal>
            <Reveal delay={80}><FeatureCard icon={Server} title="Deploy on your terms">Run the platform on infrastructure you control, alongside what you already host.</FeatureCard></Reveal>
            <Reveal delay={160}><FeatureCard icon={Fingerprint} title="Your identity, your rules">Sign-in follows the policy your organisation already enforces.</FeatureCard></Reveal>
          </div>
        </Shell>
      </Band>

      <Band tone="raised">
        <Shell>
          <SectionHead eyebrow="Who it is for" title="Where Desk Portal fits" align="center" />
          <div className="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {USE_CASES.map((u, i) => (
              <Reveal key={u.title} delay={(i % 3) * 70}>
                <FeatureCard icon={u.icon} title={u.title}>{u.body}</FeatureCard>
              </Reveal>
            ))}
          </div>
        </Shell>
      </Band>

      <Band>
        <Shell>
          <SectionHead eyebrow="Why MSPs use it" title="Designed around how your team actually works." align="center" />
          <div className="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {BENEFITS.map((b, i) => (
              <Reveal key={b.title} delay={(i % 3) * 70}>
                <FeatureCard icon={b.icon} title={b.title}>{b.body}</FeatureCard>
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
              Keep your PSA. Upgrade your client experience.
            </h2>
            <p className="mt-4 max-w-xl text-[16px] leading-relaxed text-brand-fg/75">
              Desk Portal gives your clients a modern support experience while your team continues
              working in the PSA they already know.
            </p>
            <div className="mt-8 flex flex-wrap gap-3">
              <Link
                href="/book"
                className="inline-flex items-center gap-2 rounded-xl bg-brand-fg px-6 py-3.5 text-sm font-medium text-brand transition-transform hover:-translate-y-0.5"
              >
                Book a demo <ArrowRight size={15} aria-hidden="true" />
              </Link>
              <Link
                href="#integrations"
                className="inline-flex items-center gap-2 rounded-xl border border-brand-fg/30 px-6 py-3.5 text-sm font-medium transition-colors hover:bg-brand-fg/10"
              >
                Explore integrations <ArrowDown size={15} aria-hidden="true" />
              </Link>
            </div>
          </div>
        </Reveal>
      </Shell>
    </>
  );
}
