import {
  Ticket, MessageSquare, Paperclip, CircleDot, Flag, UserCheck, Timer, StickyNote,
  ArrowLeftRight, ArrowRight, Plug, CheckCircle2,
} from 'lucide-react';

const LANES = [
  { icon: Ticket, label: 'Tickets', dir: 'both' },
  { icon: MessageSquare, label: 'Replies and notes', dir: 'both' },
  { icon: Paperclip, label: 'Attachments', dir: 'both' },
  { icon: CircleDot, label: 'Status', dir: 'both' },
  { icon: Flag, label: 'Priority', dir: 'both' },
  { icon: UserCheck, label: 'Assignment', dir: 'both' },
  { icon: Timer, label: 'Time entries', dir: 'both' },
  { icon: StickyNote, label: 'Deletions', dir: 'both' },
] as const;

/**
 * Two-way sync as a set of lanes rather than a sentence.
 *
 * One row per thing that moves, with a packet crossing it continuously. Naming each item is the
 * point: "two-way sync" is a claim, whereas a row labelled "time entries" is a specific promise a
 * buyer can check against their own workflow.
 */
export function SyncLanes() {
  return (
    <div className="rounded-2xl border border-[var(--border)] bg-[var(--surface)] p-5 sm:p-7">
      <div className="mb-5 flex items-center justify-between gap-4 text-[13px] font-semibold">
        <span className="flex items-center gap-2">
          <span className="h-2 w-2 rounded-full bg-brand" aria-hidden="true" />
          Desk Portal
        </span>
        <span className="flex items-center gap-1.5 text-[11.5px] font-medium text-[var(--muted)]">
          <ArrowLeftRight size={13} className="text-brand-mid" aria-hidden="true" />
          continuous, both directions
        </span>
        <span className="flex items-center gap-2 text-right">
          Autotask · ConnectWise
          <span className="h-2 w-2 rounded-full bg-brand-mid" aria-hidden="true" />
        </span>
      </div>

      <ul className="space-y-2.5">
        {LANES.map(({ icon: Icon, label }, i) => (
          <li key={label} className="grid grid-cols-[auto_1fr] items-center gap-3 sm:grid-cols-[11rem_1fr]">
            <span className="flex items-center gap-2 text-[13px]">
              <Icon size={14} className="shrink-0 text-brand-mid" aria-hidden="true" />
              <span className="truncate">{label}</span>
            </span>

            <span className="relative h-6 overflow-hidden rounded-full bg-[var(--bg)]" aria-hidden="true">
              <span className="absolute inset-x-3 top-1/2 h-px -translate-y-1/2 bg-[var(--border)]" />
              <span
                className="dp-lane absolute inset-y-0 left-0 w-full"
                style={{ animationDelay: `${-i * 0.32}s` }}
              >
                <span className="absolute left-2 top-1/2 h-1.5 w-1.5 -translate-y-1/2 rounded-full bg-brand" />
              </span>
              <span
                className="dp-lane-back absolute inset-y-0 left-0 w-full"
                style={{ animationDelay: `${-i * 0.45}s` }}
              >
                <span className="absolute left-2 top-1/2 h-1.5 w-1.5 -translate-y-1/2 rounded-full bg-brand-mid/70" />
              </span>
            </span>
          </li>
        ))}
      </ul>

      <p className="mt-5 text-[12.5px] text-[var(--muted)]">
        Changes made in the PSA appear in the portal, and changes made in the portal are written
        back. Your PSA stays the system of record throughout.
      </p>
    </div>
  );
}

const CARD_ITEMS = ['Tickets', 'Notes', 'Files', 'Time', 'Status', 'Priority'];

function IntegrationCard({ name, detail }: { name: string; detail: string }) {
  return (
    <div className="dp-lift rounded-2xl border border-[var(--border)] bg-[var(--surface)] p-6">
      <div className="flex items-start justify-between gap-4">
        <div className="flex items-center gap-3">
          <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-brand-tint text-brand-deep dark:bg-brand/25 dark:text-brand-soft">
            <Plug size={19} aria-hidden="true" />
          </span>
          <div>
            <h3 className="text-[15px] font-semibold">{name}</h3>
            <p className="text-[12.5px] text-[var(--muted)]">{detail}</p>
          </div>
        </div>
        <span className="inline-flex shrink-0 items-center gap-1.5 rounded-full bg-brand-tint px-2.5 py-1 text-[11px] font-medium text-brand-deep dark:bg-brand/25 dark:text-brand-soft">
          <span className="h-1.5 w-1.5 rounded-full bg-emerald-500 dp-pulse" aria-hidden="true" />
          Connected
        </span>
      </div>

      <div className="relative my-5 h-7 overflow-hidden rounded-full bg-[var(--bg)]" aria-hidden="true">
        <span className="absolute inset-x-3 top-1/2 h-px -translate-y-1/2 bg-[var(--border)]" />
        <span className="dp-lane absolute inset-y-0 left-0 w-full">
          <span className="absolute left-2.5 top-1/2 flex h-5 -translate-y-1/2 items-center rounded-full bg-brand px-2 text-[10px] font-semibold text-brand-fg">
            sync
          </span>
        </span>
      </div>

      <ul className="flex flex-wrap gap-1.5">
        {CARD_ITEMS.map((i) => (
          <li
            key={i}
            className="inline-flex items-center gap-1 rounded-full border border-[var(--border)] px-2.5 py-1 text-[11.5px] text-[var(--muted)]"
          >
            <CheckCircle2 size={11} className="text-brand-mid" aria-hidden="true" /> {i}
          </li>
        ))}
      </ul>
    </div>
  );
}

export function IntegrationCards() {
  return (
    <div className="grid gap-5 lg:grid-cols-2">
      <IntegrationCard name="Datto Autotask PSA" detail="REST API · boards, queues, work types" />
      <IntegrationCard name="ConnectWise Manage" detail="REST API · service boards, members" />
    </div>
  );
}

/** Small inline badge used in the hero and navigation areas. */
export function IntegrationBadge() {
  return (
    <span className="inline-flex items-center gap-2 rounded-full border border-[var(--border)] bg-[var(--surface)]/80 px-3 py-1.5 text-[11.5px] font-medium text-[var(--muted)] backdrop-blur">
      <span className="font-semibold text-[var(--fg)]">Autotask</span>
      <ArrowRight size={11} className="text-brand-mid" aria-hidden="true" />
      <span className="font-semibold text-[var(--fg)]">ConnectWise</span>
      <span className="hidden sm:inline">· two-way sync</span>
    </span>
  );
}
