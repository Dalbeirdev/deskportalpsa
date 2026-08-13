import {
  Paperclip, Clock, CircleDot, ShieldCheck, Users, BarChart3,
  MessageSquare, CheckCircle2, Timer, Building2,
} from 'lucide-react';

/**
 * Product mockups built from real markup rather than screenshots.
 *
 * A picture of a UI goes stale the moment the product moves, cannot be read by a screen reader,
 * and ships a large image. These render at any width, invert with the theme, and cost nothing.
 * The data is deliberately specific — "VPN connection issue", a named technician, a real SLA
 * clock — because plausible detail is what makes a visitor believe they are seeing the product.
 */

export type ProductView =
  | 'tickets' | 'conversation' | 'files' | 'timeline' | 'admin' | 'reporting' | 'client';

const STATUS: Record<string, string> = {
  'In progress': 'bg-brand-tint text-brand-deep dark:bg-brand/30 dark:text-brand-soft',
  'Waiting on client': 'bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300',
  New: 'bg-sky-100 text-sky-800 dark:bg-sky-950 dark:text-sky-300',
  Resolved: 'bg-slate-200 text-slate-700 dark:bg-slate-800 dark:text-slate-300',
};

const PRIORITY: Record<string, string> = {
  High: 'text-rose-600 dark:text-rose-400',
  Normal: 'text-[var(--muted)]',
  Critical: 'text-rose-700 dark:text-rose-300',
};

const TICKETS = [
  { id: '10482', title: 'VPN connection issue', status: 'In progress', priority: 'High', who: 'Michael', when: '4m' },
  { id: '10479', title: 'Outlook not syncing on new laptop', status: 'Waiting on client', priority: 'Normal', who: 'Priya', when: '1h' },
  { id: '10475', title: 'Add user to finance share', status: 'New', priority: 'Normal', who: 'Unassigned', when: '2h' },
  { id: '10468', title: 'Printer offline — 2nd floor', status: 'Resolved', priority: 'Normal', who: 'Sam', when: '1d' },
];

/** Browser chrome. Sells "this is a real screen" more cheaply than any other device. */
export function BrowserFrame({
  children, url = 'portal.yourmsp.com/tickets', className = '',
}: { children: React.ReactNode; url?: string; className?: string }) {
  return (
    <div
      className={`overflow-hidden rounded-2xl border border-[var(--border)] bg-[var(--surface)] shadow-[0_24px_60px_-24px_rgba(11,18,32,0.35)] ${className}`}
    >
      <div className="flex items-center gap-2 border-b border-[var(--border)] bg-[var(--bg)] px-3.5 py-2.5">
        <span className="flex gap-1.5" aria-hidden="true">
          {['bg-rose-400', 'bg-amber-400', 'bg-emerald-400'].map((c) => (
            <span key={c} className={`h-2.5 w-2.5 rounded-full ${c}`} />
          ))}
        </span>
        <span className="mx-auto flex items-center gap-1.5 rounded-md bg-[var(--surface)] px-3 py-1 text-[11px] text-[var(--muted)]">
          <ShieldCheck size={11} className="text-brand-mid" aria-hidden="true" />
          {url}
        </span>
      </div>
      {children}
    </div>
  );
}

function Row({ t, active }: { t: (typeof TICKETS)[number]; active?: boolean }) {
  return (
    <div
      className={`grid grid-cols-[1fr_auto] items-center gap-3 rounded-lg px-3 py-2.5 ${
        active ? 'bg-brand-tint dark:bg-brand/20' : ''
      }`}
    >
      <div className="min-w-0">
        <p className="truncate text-[13px] font-medium">
          <span className="text-[var(--faint)]">#{t.id}</span> {t.title}
        </p>
        <p className="mt-1 flex items-center gap-2 text-[11px] text-[var(--muted)]">
          <span className={`rounded px-1.5 py-0.5 font-medium ${STATUS[t.status]}`}>{t.status}</span>
          <span className={PRIORITY[t.priority]}>{t.priority}</span>
          <span className="hidden sm:inline">· {t.who}</span>
        </p>
      </div>
      <span className="text-[11px] tabular-nums text-[var(--faint)]">{t.when}</span>
    </div>
  );
}

function Panel({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="rounded-xl border border-[var(--border)] p-3">
      <p className="mb-2 text-[11px] font-semibold uppercase tracking-wider text-[var(--faint)]">{title}</p>
      {children}
    </div>
  );
}

export function ProductScreen({ view = 'tickets' }: { view?: ProductView }) {
  if (view === 'conversation') {
    return (
      <div className="space-y-3 p-4">
        <div className="flex items-center justify-between">
          <p className="text-sm font-semibold">#10482 · VPN connection issue</p>
          <span className={`rounded px-1.5 py-0.5 text-[11px] font-medium ${STATUS['In progress']}`}>In progress</span>
        </div>
        <div className="space-y-2.5">
          {[
            { who: 'Dana (client)', mine: false, body: 'Cannot connect to the VPN since this morning — it times out at 90%.' },
            { who: 'Michael (technician)', mine: true, body: 'Thanks Dana. Working on this now — checking the firewall rules.' },
            { who: 'Dana (client)', mine: false, body: 'Screenshot of the error attached.' },
          ].map((m) => (
            <div key={m.body} className={`max-w-[85%] rounded-xl px-3 py-2 text-[12.5px] leading-relaxed ${
              m.mine ? 'ml-auto bg-brand text-brand-fg' : 'bg-[var(--bg)]'
            }`}>
              <p className={`mb-0.5 text-[10.5px] font-semibold ${m.mine ? 'text-brand-fg/70' : 'text-[var(--faint)]'}`}>{m.who}</p>
              {m.body}
            </div>
          ))}
        </div>
        <div className="flex items-center gap-2 rounded-lg border border-[var(--border)] px-3 py-2 text-[11px] text-[var(--muted)]">
          <MessageSquare size={12} aria-hidden="true" /> Internal notes stay internal — never shown to the client.
        </div>
      </div>
    );
  }

  if (view === 'files') {
    return (
      <div className="space-y-2.5 p-4">
        <p className="text-sm font-semibold">Attachments on #10482</p>
        {[
          { n: 'vpn-error-90-percent.png', s: '412 KB', from: 'Dana · client portal' },
          { n: 'firewall-rule-export.csv', s: '18 KB', from: 'Michael · Autotask' },
          { n: 'resolution-steps.pdf', s: '96 KB', from: 'Michael · Autotask' },
        ].map((f) => (
          <div key={f.n} className="flex items-center gap-3 rounded-lg border border-[var(--border)] px-3 py-2.5">
            <Paperclip size={14} className="shrink-0 text-brand-mid" aria-hidden="true" />
            <span className="min-w-0 flex-1">
              <span className="block truncate text-[12.5px] font-medium">{f.n}</span>
              <span className="block text-[11px] text-[var(--muted)]">{f.s} · {f.from}</span>
            </span>
            <CheckCircle2 size={14} className="shrink-0 text-brand-mid" aria-hidden="true" />
          </div>
        ))}
      </div>
    );
  }

  if (view === 'timeline') {
    return (
      <div className="p-4">
        <p className="mb-3 text-sm font-semibold">Ticket timeline</p>
        <ol className="relative space-y-3 border-l border-[var(--border)] pl-4">
          {[
            { t: '09:14', e: 'Client submitted request', s: 'Desk Portal' },
            { t: '09:14', e: 'Ticket created', s: 'Autotask · board Service Desk' },
            { t: '09:31', e: 'Assigned to Michael', s: 'Autotask' },
            { t: '10:02', e: 'Technician replied', s: 'Autotask → portal' },
            { t: '10:05', e: 'Screenshot attached', s: 'Portal → Autotask' },
            { t: '10:40', e: '0.75 h logged, billable', s: 'Portal → Autotask' },
          ].map((x) => (
            <li key={x.t + x.e} className="relative">
              <span className="absolute -left-[21px] top-1.5 h-2 w-2 rounded-full bg-brand-mid" aria-hidden="true" />
              <p className="text-[12.5px] font-medium">{x.e}</p>
              <p className="text-[11px] text-[var(--muted)]">{x.t} · {x.s}</p>
            </li>
          ))}
        </ol>
      </div>
    );
  }

  if (view === 'admin') {
    return (
      <div className="space-y-3 p-4">
        <p className="text-sm font-semibold">PSA connections</p>
        {[
          { n: 'Autotask — Techpio', s: 'Healthy', last: 'synced 2m ago' },
          { n: 'ConnectWise — Northwind', s: 'Healthy', last: 'synced 4m ago' },
        ].map((c) => (
          <div key={c.n} className="flex items-center gap-3 rounded-lg border border-[var(--border)] px-3 py-2.5">
            <span className="h-2 w-2 rounded-full bg-emerald-500 dp-pulse" aria-hidden="true" />
            <span className="min-w-0 flex-1">
              <span className="block truncate text-[12.5px] font-medium">{c.n}</span>
              <span className="block text-[11px] text-[var(--muted)]">{c.s} · {c.last}</span>
            </span>
          </div>
        ))}
        <div className="grid grid-cols-2 gap-2.5">
          <Panel title="Status mapping"><p className="text-[12px] text-[var(--muted)]">New → New (not responded)</p></Panel>
          <Panel title="Default board"><p className="text-[12px] text-[var(--muted)]">Service Desk</p></Panel>
        </div>
      </div>
    );
  }

  if (view === 'reporting') {
    return (
      <div className="space-y-3 p-4">
        <p className="text-sm font-semibold">This week</p>
        <div className="grid grid-cols-3 gap-2.5">
          {[
            { l: 'Resolved', v: '38', i: CheckCircle2 },
            { l: 'Hours logged', v: '52.5', i: Timer },
            { l: 'SLA met', v: '94%', i: BarChart3 },
          ].map(({ l, v, i: I }) => (
            <div key={l} className="rounded-xl border border-[var(--border)] p-3">
              <I size={14} className="text-brand-mid" aria-hidden="true" />
              <p className="mt-1.5 text-lg font-semibold tabular-nums">{v}</p>
              <p className="text-[11px] text-[var(--muted)]">{l}</p>
            </div>
          ))}
        </div>
        <div className="flex h-24 items-end gap-1.5 rounded-xl border border-[var(--border)] p-3">
          {[40, 62, 48, 75, 58, 88, 70].map((h, i) => (
            <span key={i} className="flex-1 rounded-t bg-brand-mid/70" style={{ height: `${h}%` }} aria-hidden="true" />
          ))}
        </div>
        <p className="text-[11px] text-[var(--muted)]">Illustrative figures from a sample workspace.</p>
      </div>
    );
  }

  if (view === 'client') {
    return (
      <div className="space-y-3 p-4">
        <div className="flex items-center gap-2">
          <Building2 size={14} className="text-brand-mid" aria-hidden="true" />
          <p className="text-sm font-semibold">Acme Ltd · your requests</p>
        </div>
        {TICKETS.slice(0, 3).map((t) => <Row key={t.id} t={t} />)}
        <button className="w-full rounded-lg bg-brand px-3 py-2.5 text-[12.5px] font-medium text-brand-fg">
          New request
        </button>
        <p className="text-[11px] text-[var(--muted)]">
          Clients see only their own company&rsquo;s tickets.
        </p>
      </div>
    );
  }

  return (
    <div className="grid gap-0 sm:grid-cols-[1.15fr_1fr]">
      <div className="border-b border-[var(--border)] p-3 sm:border-b-0 sm:border-r">
        <div className="mb-2 flex items-center justify-between px-1">
          <p className="text-[11px] font-semibold uppercase tracking-wider text-[var(--faint)]">Open tickets</p>
          <span className="flex items-center gap-1 text-[10.5px] text-brand-mid">
            <CircleDot size={10} className="dp-pulse" aria-hidden="true" /> live
          </span>
        </div>
        <div className="space-y-1">
          {TICKETS.map((t, i) => <Row key={t.id} t={t} active={i === 0} />)}
        </div>
      </div>

      <div className="space-y-2.5 p-3">
        <div>
          <p className="text-[13px] font-semibold">#10482 · VPN connection issue</p>
          <p className="mt-1 text-[11px] text-[var(--muted)]">Acme Ltd · Dana Reed</p>
        </div>
        <div className="grid grid-cols-2 gap-2">
          <Panel title="Assigned">
            <p className="flex items-center gap-1.5 text-[12px] font-medium">
              <Users size={12} className="text-brand-mid" aria-hidden="true" /> Michael
            </p>
          </Panel>
          <Panel title="SLA">
            <p className="flex items-center gap-1.5 text-[12px] font-medium">
              <Clock size={12} className="text-brand-mid" aria-hidden="true" /> 2h 12m left
            </p>
          </Panel>
        </div>
        <Panel title="Last reply">
          <p className="text-[12px] leading-relaxed text-[var(--muted)]">
            &ldquo;Working on this now — checking the firewall rules.&rdquo;
          </p>
        </Panel>
        <div className="flex items-center gap-2 rounded-xl border border-[var(--border)] px-3 py-2">
          <Paperclip size={12} className="text-brand-mid" aria-hidden="true" />
          <span className="truncate text-[11.5px] text-[var(--muted)]">vpn-error-90-percent.png</span>
        </div>
      </div>
    </div>
  );
}
