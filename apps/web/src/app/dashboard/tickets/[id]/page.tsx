'use client';

import { use, useRef, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ArrowLeft, ChevronLeft, ChevronRight, ChevronDown, Pencil, MoreHorizontal, Paperclip,
  Send, Bold, Smile, Link2, ArrowUpDown, Lock, Monitor, Wifi, Mail, KeyRound, Cpu, Ticket,
  Copy, RefreshCw, Download, Clock, Trash2, Check, X, ClipboardList, UserCog, ExternalLink} from 'lucide-react';
import { useTimer } from '@/components/TimerProvider';
import { api, type AssigneeOptions } from '@/lib/api';
import type { TicketDetail } from '@/lib/types';

const STATUS_TONE: Record<string, string> = {
  NEW: 'bg-blue-100 text-blue-700 dark:bg-blue-950 dark:text-blue-300',
  IN_PROGRESS: 'bg-amber-100 text-amber-700 dark:bg-amber-950 dark:text-amber-300',
  WAITING_CUSTOMER: 'bg-violet-100 text-violet-700 dark:bg-violet-950 dark:text-violet-300',
  ON_HOLD: 'bg-orange-100 text-orange-700 dark:bg-orange-950 dark:text-orange-300',
  RESOLVED: 'bg-green-100 text-green-700 dark:bg-green-950 dark:text-green-300',
  CLOSED: 'bg-slate-200 text-slate-600 dark:bg-slate-800 dark:text-slate-300',
};
const STATUSES = ['NEW', 'IN_PROGRESS', 'WAITING_CUSTOMER', 'ON_HOLD', 'RESOLVED', 'CLOSED'];
const PRIORITY_TONE: Record<string, string> = {
  LOW: 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300',
  NORMAL: 'bg-blue-100 text-blue-700 dark:bg-blue-950 dark:text-blue-300',
  HIGH: 'bg-orange-100 text-orange-700 dark:bg-orange-950 dark:text-orange-300',
  CRITICAL: 'bg-red-100 text-red-700 dark:bg-red-950 dark:text-red-300',
};
function categoryIcon(cat?: string | null, title?: string) {
  const s = `${cat ?? ''} ${title ?? ''}`.toLowerCase();
  if (/password|access|login|account|vpn/.test(s)) return Lock;
  if (/network|wifi|wi-fi|firewall/.test(s)) return Wifi;
  if (/email|outlook|mail|365/.test(s)) return Mail;
  if (/hardware|printer|laptop|monitor|disk/.test(s)) return Monitor;
  if (/software|application|install/.test(s)) return Cpu;
  if (/key|reset/.test(s)) return KeyRound;
  return Ticket;
}
function fmt(iso: string, seconds = false): string {
  const d = new Date(iso);
  const date = d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  const time = d.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit', ...(seconds ? { second: '2-digit' } : {}) });
  return `${date} · ${time}`;
}
const initials = (name: string) => name.split(' ').map((n) => n[0]).join('').slice(0, 2).toUpperCase();

// PSAs record the day time was worked, not the moment. Rendering a clock time turns a midnight
// placeholder into a claim the technician worked at 5:30am.
const fmtDay = (iso: string) =>
  new Date(iso).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });

// Sub-kilobyte files round to "0 KB", which reads as an empty upload.
function fmtSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

// Technicians think in minutes; a bare "0.17h" is unreadable at a glance. Show both, as the PSA's
// own decimal is what gets invoiced.
function fmtDuration(hours: number) {
  const mins = Math.round(hours * 60);
  if (mins < 60) return `${mins}m`;
  const h = Math.floor(mins / 60);
  const m = mins % 60;
  return m ? `${h}h ${m}m` : `${h}h`;
}

type TicketAttachment = TicketDetail['attachments'][number];

/**
 * Picks who works the ticket and which queue it sits on. Roles are shown next to each name because
 * "who can take this" is a role question first — an Engineer and a Help Desk tech covering the same
 * board are not interchangeable, and the provider only exposes that through queue coverage.
 */
function AssignPanel({ options, currentTechnicianId, currentQueueId, pending, error, onCancel, onSave }: {
  options: AssigneeOptions | undefined;
  currentTechnicianId: string | null;
  currentQueueId: string | null;
  pending: boolean;
  error: string | null;
  onCancel: () => void;
  onSave: (body: { technicianExternalId?: string; queueOrBoardId?: string; roleId?: string }) => void;
}) {
  const [technician, setTechnician] = useState(currentTechnicianId ?? '');
  const [queue, setQueue] = useState('');
  const [role, setRole] = useState('');

  if (!options) return <p className="text-xs text-[var(--muted)]">Loading technicians…</p>;

  // Only worth asking when the person genuinely holds more than one role here; otherwise the
  // server picks the single role they have on this queue and the field is noise.
  const roleOptions = options.technicians.find((t) => t.id === technician)?.roleOptions ?? [];

  const changed = (technician && technician !== currentTechnicianId) || (queue && queue !== currentQueueId);
  return (
    <div className="space-y-3">
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        <label className="block">
          <span className="mb-1 block text-xs font-medium">Technician</span>
          <select value={technician} onChange={(e) => { setTechnician(e.target.value); setRole(''); }}
            className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm outline-none focus:border-brand">
            <option value="">— unchanged —</option>
            {options.technicians.map((t) => (
              <option key={t.id} value={t.id}>{t.roles.length ? `${t.name} · ${t.roles.join(', ')}` : t.name}</option>
            ))}
          </select>
          <span className="mt-1 block text-xs text-[var(--muted)]">
            {options.filteredByQueue
              ? 'Technicians who cover this queue, with the role they hold on it.'
              : options.filteredByRole
                ? 'Technicians who hold a role in the PSA. This queue has no specific coverage, so all of them are listed.'
                : 'This PSA does not publish role or queue coverage, so everyone is listed.'}
          </span>
        </label>
        {roleOptions.length > 1 && (
          <label className="block">
            <span className="mb-1 block text-xs font-medium">Role</span>
            <select value={role} onChange={(e) => setRole(e.target.value)}
              className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm outline-none focus:border-brand">
              <option value="">— their role on this queue —</option>
              {roleOptions.map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}
            </select>
            <span className="mt-1 block text-xs text-[var(--muted)]">They hold several — pick which one they take this in.</span>
          </label>
        )}
        <label className="block">
          <span className="mb-1 block text-xs font-medium">Queue / board</span>
          <select value={queue} onChange={(e) => setQueue(e.target.value)}
            className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm outline-none focus:border-brand">
            <option value="">— unchanged —</option>
            {options.queuesOrBoards.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
          </select>
          <span className="mt-1 block text-xs text-[var(--muted)]">Moving a ticket can change who covers it.</span>
        </label>
      </div>
      {error && <p className="text-xs text-red-600 dark:text-red-400">{error}</p>}
      <div className="flex items-center gap-2">
        <button
          onClick={() => onSave({
            technicianExternalId: technician || undefined,
            queueOrBoardId: queue || undefined,
            roleId: role || undefined,
          })}
          disabled={pending || !changed}
          className="inline-flex items-center gap-1.5 rounded-lg bg-brand px-3 py-2 text-sm font-medium text-brand-fg hover:opacity-90 disabled:opacity-50">
          <Check size={15} /> {pending ? 'Saving…' : 'Save assignment'}
        </button>
        <button onClick={onCancel} className="rounded-lg border border-[var(--border)] px-3 py-2 text-sm hover:bg-[var(--bg)]">Cancel</button>
      </div>
    </div>
  );
}

/** One file, rendered the same way under a reply and in the loose-files list. */
function AttachmentChip({ a, provider, onDownload }: {
  a: TicketAttachment; provider: number; onDownload: (id: string) => void;
}) {
  const clean = String(a.scanStatus) === '1' || String(a.scanStatus) === 'Clean';
  return (
    <span className="inline-flex max-w-full items-center gap-1.5 rounded-lg border border-[var(--border)] bg-[var(--bg)] px-2 py-1 text-xs">
      <Paperclip size={12} className="shrink-0 text-[var(--muted)]" />
      <span className="truncate">{a.fileName}</span>
      {a.fromProvider && (
        <span title={a.authorName ? `Attached by ${a.authorName}` : undefined} className="shrink-0 text-[var(--faint)]">
          · {providerLabel(provider)}
        </span>
      )}
      <span className="shrink-0 text-[var(--faint)]">{fmtSize(a.sizeBytes)}</span>
      {clean
        ? <button onClick={() => onDownload(a.id)} aria-label={`Download ${a.fileName}`}
            className="shrink-0 rounded p-0.5 text-[var(--muted)] hover:text-brand"><Download size={13} /></button>
        : <span className="shrink-0 rounded bg-red-100 px-1 py-0.5 font-medium text-red-700 dark:bg-red-950 dark:text-red-300">Quarantined</span>}
    </span>
  );
}

// PSAs distinguish "do not bill" from "no charge"; the boolean alone flattens that away.
function billableLabel(option: string, billable: boolean) {
  if (option === 'NoCharge') return 'No charge';
  if (option === 'DoNotBill') return 'Do not bill';
  return billable ? 'Billable' : 'No charge';
}

// ProviderType: 1 = ConnectWise, 2 = Autotask.
const providerLabel = (provider: number) => (provider === 1 ? 'ConnectWise' : provider === 2 ? 'Autotask' : 'the PSA');
const providerAbbrev = (provider: number) => (provider === 1 ? 'CW' : provider === 2 ? 'AT' : 'PSA');

export default function TicketDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const qc = useQueryClient();
  const router = useRouter();
  const [comment, setComment] = useState('');
  const [uploading, setUploading] = useState(false);
  // Files chosen in the composer are held until the reply is sent, so they can be filed against
  // that message rather than dropped loose on the ticket.
  const [pendingFiles, setPendingFiles] = useState<File[]>([]);
  const [assignOpen, setAssignOpen] = useState(false);
  const [dragOver, setDragOver] = useState(false);
  const [oldestFirst, setOldestFirst] = useState(true);
  const [menuOpen, setMenuOpen] = useState(false);
  const [hours, setHours] = useState('');
  const [billable, setBillable] = useState('Billable');
  const [timeNotes, setTimeNotes] = useState('');
  const [workType, setWorkType] = useState('');
  const [workRole, setWorkRole] = useState('');
  const [editEntry, setEditEntry] = useState<{ id: string; hours: string; notes: string } | null>(null);
  const timer = useTimer();
  function applyTimer() {
    const rounded = Math.max(0.25, Math.round((timer.seconds / 3600) / 0.25) * 0.25); // nearest 0.25h, min 15 min
    setHours(rounded.toFixed(2));
    timer.pause();
  }
  const fileRef = useRef<HTMLInputElement>(null);

  const { data: ticket, isLoading, isError } = useQuery({ queryKey: ['ticket', id], queryFn: () => api.getTicket(id) });
  const { data: list } = useQuery({ queryKey: ['tickets'], queryFn: api.listTickets });
  // Only fetched once the picker is opened: it costs a provider round trip for coverage data that
  // most visits to a ticket never need.
  const { data: assignOpts } = useQuery({
    queryKey: ['assignees', id],
    queryFn: () => api.ticketAssignees(id),
    enabled: assignOpen,
    retry: false,
  });
  const assign = useMutation({
    mutationFn: (body: { technicianExternalId?: string; queueOrBoardId?: string; roleId?: string }) => api.assignTicket(id, body),
    onSuccess: () => {
      setAssignOpen(false);
      [['ticket', id], ['tickets'], ['team']].forEach((k) => qc.invalidateQueries({ queryKey: k }));
    },
  });

  const { data: timeOpts } = useQuery({ queryKey: ['time-options', id], queryFn: () => api.ticketTimeOptions(id), enabled: !!ticket, retry: false });

  function startTimerHere() {
    if (!ticket) return;
    if (timer.running && timer.target?.ticketId !== id &&
        !window.confirm('A timer is already running for another ticket. Attach it to this one?')) return;
    timer.attach({ ticketId: id, ref: ticket.externalTicketId, title: ticket.title });
    timer.start();
  }

  const addComment = useMutation({
    mutationFn: async (body: string) => {
      const note = await api.addComment(id, body);
      // Upload after the reply exists, so each file carries its note id all the way to the PSA.
      for (const file of pendingFiles) await api.uploadAttachment(id, file, note.id);
      return note;
    },
    onSuccess: () => { setComment(''); setPendingFiles([]); qc.invalidateQueries({ queryKey: ['ticket', id] }); },
  });

  const { data: entries } = useQuery({ queryKey: ['time-entries', id], queryFn: () => api.listTimeEntries(id), enabled: !!ticket, retry: false });

  const refreshTime = () =>
    [['time-entries', id], ['ticket', id], ['team'], ['trend']].forEach((k) => qc.invalidateQueries({ queryKey: k }));

  const logTime = useMutation({
    mutationFn: () => api.logTime(id, { hours: parseFloat(hours), billable, notes: timeNotes || undefined, workType: workType || undefined, workRole: workRole || undefined }),
    onSuccess: () => { setHours(''); setTimeNotes(''); refreshTime(); },
  });
  const statusMut = useMutation({
    mutationFn: (status: string) => api.updateTicketStatus(id, status),
    onSuccess: () => { [['ticket', id], ['tickets'], ['team'], ['trend']].forEach((k) => qc.invalidateQueries({ queryKey: k })); },
  });
  const retryEntry = useMutation({
    mutationFn: (entryId: string) => api.retryTimeEntry(id, entryId),
    onSuccess: refreshTime,
  });
  const delEntry = useMutation({
    mutationFn: (entryId: string) => api.deleteTimeEntry(id, entryId),
    onSuccess: refreshTime,
  });
  const updEntry = useMutation({
    mutationFn: (v: { entryId: string; hours: number; notes: string }) => api.updateTimeEntry(id, v.entryId, { hours: v.hours, notes: v.notes }),
    onSuccess: () => { setEditEntry(null); refreshTime(); },
  });

  async function upload(file: File) {
    setUploading(true);
    try { await api.uploadAttachment(id, file); qc.invalidateQueries({ queryKey: ['ticket', id] }); }
    catch { /* surfaced by the disabled state */ }
    finally { setUploading(false); }
  }
  async function download(attachmentId: string) {
    try { const { url } = await api.attachmentDownloadUrl(id, attachmentId); window.open(url, '_blank', 'noopener'); } catch { /* */ }
  }

  // Prev/next navigation across the ticket list (ordered newest-first by the API).
  const idx = list?.findIndex((t) => t.id === id) ?? -1;
  const prev = idx > 0 ? list?.[idx - 1] : undefined;
  const next = idx >= 0 && list ? list[idx + 1] : undefined;

  return (
    <div className="mx-auto max-w-4xl space-y-4">
      {/* Header controls */}
      <div className="flex flex-wrap items-center justify-between gap-2">
        <Link href="/dashboard/tickets" className="inline-flex items-center gap-1.5 text-sm text-[var(--muted)] hover:text-[var(--fg)]">
          <ArrowLeft size={16} /> Back to tickets
        </Link>
        <div className="flex items-center gap-2">
          <div className="relative">
            <button onClick={() => setMenuOpen((v) => !v)} onBlur={() => setTimeout(() => setMenuOpen(false), 150)}
              className="inline-flex items-center gap-1.5 rounded-lg border border-[var(--border)] bg-[var(--surface)] px-3 py-1.5 text-sm font-medium hover:bg-[var(--bg)]">
              <MoreHorizontal size={15} /> More actions <ChevronDown size={13} className="text-[var(--faint)]" />
            </button>
            {menuOpen && (
              <div className="absolute right-0 z-10 mt-1 w-48 overflow-hidden rounded-lg border border-[var(--border)] bg-[var(--surface)] py-1 text-sm shadow-lg">
                <button onClick={() => { if (ticket?.externalTicketId) navigator.clipboard?.writeText(ticket.externalTicketId); }} className="flex w-full items-center gap-2 px-3 py-2 hover:bg-[var(--bg)]"><Copy size={14} /> Copy reference</button>
                <button onClick={() => qc.invalidateQueries({ queryKey: ['ticket', id] })} className="flex w-full items-center gap-2 px-3 py-2 hover:bg-[var(--bg)]"><RefreshCw size={14} /> Refresh</button>
                <Link href="/dashboard/tickets" className="flex w-full items-center gap-2 px-3 py-2 hover:bg-[var(--bg)]"><ArrowLeft size={14} /> Back to tickets</Link>
              </div>
            )}
          </div>
          <div className="flex items-center">
            <button disabled={!prev} onClick={() => prev && router.push(`/dashboard/tickets/${prev.id}`)} aria-label="Previous ticket"
              className="rounded-l-lg border border-[var(--border)] bg-[var(--surface)] p-2 text-[var(--muted)] hover:bg-[var(--bg)] disabled:opacity-40"><ChevronLeft size={16} /></button>
            <button disabled={!next} onClick={() => next && router.push(`/dashboard/tickets/${next.id}`)} aria-label="Next ticket"
              className="rounded-r-lg border border-l-0 border-[var(--border)] bg-[var(--surface)] p-2 text-[var(--muted)] hover:bg-[var(--bg)] disabled:opacity-40"><ChevronRight size={16} /></button>
          </div>
        </div>
      </div>

      {isLoading && <div className="h-48 animate-pulse rounded-xl border border-[var(--border)] bg-[var(--surface)]" />}
      {isError && <div className="rounded-xl border border-dashed border-[var(--border)] p-8 text-center text-sm text-[var(--muted)]">Couldn&apos;t load this ticket — is the API running?</div>}

      {ticket && (() => {
        const Icon = categoryIcon(ticket.portalCategory, ticket.title);
        // Files belong to the message they were posted with; only genuinely loose ones fall through
        // to the list at the bottom.
        const filesByNote = new Map<string, TicketAttachment[]>();
        for (const a of ticket.attachments) {
          if (!a.ticketNoteId) continue;
          const bucket = filesByNote.get(a.ticketNoteId) ?? [];
          bucket.push(a);
          filesByNote.set(a.ticketNoteId, bucket);
        }
        const looseFiles = ticket.attachments.filter((a) => !a.ticketNoteId);

        const convo = [...ticket.conversation].sort((a, b) =>
          (oldestFirst ? 1 : -1) * (new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime()));
        return (
          <>
            {/* Ticket card */}
            <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-6">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="flex items-start gap-3">
                  <span className="flex h-12 w-12 items-center justify-center rounded-xl bg-[var(--bg)] text-[var(--muted)]"><Icon size={22} /></span>
                  <div>
                    <h1 className="text-xl font-semibold">{ticket.title}</h1>
                    <p className="whitespace-pre-line text-sm text-[var(--muted)]">{ticket.description ?? 'No description provided.'}</p>
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  <span className="relative">
                    <select value={ticket.portalStatus} disabled={statusMut.isPending}
                      onChange={(e) => statusMut.mutate(e.target.value)} aria-label="Ticket status"
                      className={`cursor-pointer appearance-none rounded-md border-0 py-1 pl-2.5 pr-7 text-xs font-semibold outline-none focus:ring-2 focus:ring-brand disabled:opacity-60 ${STATUS_TONE[ticket.portalStatus] ?? STATUS_TONE.NEW}`}>
                      {!STATUSES.includes(ticket.portalStatus) && <option value={ticket.portalStatus}>{ticket.portalStatus.replace(/_/g, ' ')}</option>}
                      {STATUSES.map((s) => <option key={s} value={s}>{s.replace(/_/g, ' ')}</option>)}
                    </select>
                    <ChevronDown size={12} className="pointer-events-none absolute right-2 top-1/2 -translate-y-1/2 opacity-70" />
                  </span>
                  <span className={`inline-flex items-center rounded-md px-2.5 py-1 text-xs font-semibold ${PRIORITY_TONE[ticket.portalPriority.toUpperCase()] ?? PRIORITY_TONE.NORMAL}`}>{ticket.portalPriority.toUpperCase()}</span>
                </div>
              </div>
              {statusMut.isError && (
                <p className="mt-2 text-right text-xs text-red-600 dark:text-red-400">
                  Couldn&apos;t change status: {statusMut.error instanceof Error ? statusMut.error.message : 'the connection is unreachable.'}
                </p>
              )}
              <dl className="mt-5 grid grid-cols-2 gap-x-6 gap-y-3 border-t border-[var(--border)] pt-4 text-sm sm:grid-cols-3 lg:grid-cols-6">
                <Meta label="Reference" value={ticket.externalTicketId ?? '—'} href={ticket.externalTicketUrl} />
                <Meta label="Source" value={ticket.connectionName ?? '—'} />
                <Meta label="Queue / Board" value={ticket.queueOrBoard ?? '—'} />
                <Meta label="Assigned to" value={ticket.assignedTechnicianName ?? ticket.assignedTechnicianExternalId ?? 'Unassigned'} />
                <Meta label="Category" value={ticket.portalCategory ?? '—'} />
                <Meta label="Customer" value={ticket.customerName ?? '—'} />
                <Meta label="Opened" value={fmt(ticket.createdAt)} />
                <Meta label="Updated" value={fmt(ticket.updatedAt)} />
              </dl>

              <div className="mt-4 border-t border-[var(--border)] pt-4">
                {!assignOpen ? (
                  <button onClick={() => setAssignOpen(true)}
                    className="inline-flex items-center gap-1.5 rounded-lg border border-[var(--border)] px-3 py-1.5 text-xs font-medium hover:bg-[var(--bg)]">
                    <UserCog size={14} /> {ticket.assignedTechnicianName ? 'Reassign or move queue' : 'Assign technician'}
                  </button>
                ) : (
                  <AssignPanel
                    options={assignOpts}
                    currentTechnicianId={ticket.assignedTechnicianExternalId}
                    currentQueueId={assignOpts?.queueOrBoardId ?? null}
                    pending={assign.isPending}
                    error={assign.isError ? (assign.error instanceof Error ? assign.error.message : 'The PSA rejected the change.') : null}
                    onCancel={() => { setAssignOpen(false); assign.reset(); }}
                    onSave={(body) => assign.mutate(body)}
                  />
                )}
              </div>
            </div>

            {/* Service instructions the client set for technicians (from the Control Panel). */}
            {ticket.serviceInstructions && (
              <div className="rounded-xl border border-amber-200 bg-amber-50 p-5 dark:border-amber-900/60 dark:bg-amber-950/30">
                <h2 className="mb-2 flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-amber-700 dark:text-amber-300">
                  <ClipboardList size={14} /> Service Instructions
                </h2>
                <p className="whitespace-pre-wrap font-mono text-sm leading-relaxed text-amber-900 dark:text-amber-100">{ticket.serviceInstructions}</p>
                <p className="mt-2 text-xs text-amber-700/70 dark:text-amber-300/60">Set by the customer in their Control Panel — follow these when working this ticket.</p>
              </div>
            )}

            {/* Log time */}
            <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5">
              <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-[var(--faint)]"><Clock size={14} /> Log time</h2>
              <form onSubmit={(e) => { e.preventDefault(); if (parseFloat(hours) > 0) logTime.mutate(); }} className="space-y-3">
                <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
                  <label className="block">
                    <span className="mb-1 block text-xs text-[var(--muted)]">Hours</span>
                    <input type="number" step="0.25" min="0" value={hours} onChange={(e) => setHours(e.target.value)} placeholder="0.5"
                      className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm outline-none focus:border-brand" />
                  </label>
                  <label className="block">
                    <span className="mb-1 block text-xs text-[var(--muted)]">Billable</span>
                    <select value={billable} onChange={(e) => setBillable(e.target.value)}
                      className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm outline-none focus:border-brand">
                      <option value="Billable">Billable</option>
                      <option value="DoNotBill">Do not bill</option>
                      <option value="NoCharge">No charge</option>
                    </select>
                  </label>
                  {timeOpts && timeOpts.workTypes.length > 0 && (
                    <label className="block">
                      <span className="mb-1 block text-xs text-[var(--muted)]">Work type</span>
                      <select value={workType} onChange={(e) => setWorkType(e.target.value)}
                        className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm outline-none focus:border-brand">
                        <option value="">—</option>
                        {timeOpts.workTypes.map((o) => <option key={o.value} value={o.label}>{o.label}</option>)}
                      </select>
                    </label>
                  )}
                  {timeOpts && timeOpts.workRoles.length > 0 && (
                    <label className="block">
                      <span className="mb-1 block text-xs text-[var(--muted)]">Work role</span>
                      <select value={workRole} onChange={(e) => setWorkRole(e.target.value)}
                        className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm outline-none focus:border-brand">
                        <option value="">—</option>
                        {timeOpts.workRoles.map((o) => <option key={o.value} value={o.label}>{o.label}</option>)}
                      </select>
                    </label>
                  )}
                </div>

                <div className="flex flex-wrap items-center gap-2">
                  <span className="text-xs text-[var(--muted)]">Global timer</span>
                  <button type="button" onClick={startTimerHere}
                    className={`inline-flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-sm font-medium ${timer.running && timer.target?.ticketId === id ? 'border-brand/40 bg-brand/5 text-brand' : 'border-[var(--border)] hover:bg-[var(--bg)]'}`}>
                    <Clock size={14} /> {timer.running && timer.target?.ticketId === id ? 'Timing…' : 'Start timer here'}
                  </button>
                  <button type="button" onClick={applyTimer} disabled={timer.seconds === 0}
                    className="inline-flex items-center gap-1.5 rounded-lg border border-[var(--border)] px-3 py-1.5 text-sm font-medium tabular-nums hover:bg-[var(--bg)] disabled:opacity-50">
                    Use {String(Math.floor(timer.seconds / 60)).padStart(2, '0')}:{String(timer.seconds % 60).padStart(2, '0')}
                  </button>
                </div>

                <label className="block">
                  <span className="mb-1 block text-xs text-[var(--muted)]">Notes</span>
                  <input value={timeNotes} onChange={(e) => setTimeNotes(e.target.value)} placeholder="What did you work on?"
                    className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm outline-none focus:border-brand" />
                </label>

                <div className="flex flex-wrap items-center justify-between gap-2">
                  <p className="text-xs text-[var(--faint)]">Posts a time entry to the PSA against this ticket. Use the timer to track live, or enter hours directly.</p>
                  <button type="submit" disabled={logTime.isPending || !(parseFloat(hours) > 0)}
                    className="inline-flex items-center gap-2 rounded-lg bg-brand px-4 py-2 text-sm font-medium text-brand-fg hover:opacity-90 disabled:opacity-50">
                    <Clock size={15} /> {logTime.isPending ? 'Logging…' : 'Log time'}
                  </button>
                </div>

                {logTime.isSuccess && logTime.data && (
                  <p className="text-xs font-medium text-green-600 dark:text-green-400">Logged to the PSA · ticket total {logTime.data.timeWorkedHours}h ({logTime.data.billableHours}h billable).</p>
                )}
                {logTime.isError && (
                  <p className="text-xs text-red-600 dark:text-red-400">Couldn&apos;t log time — the PSA rejected it or the connection is unreachable.</p>
                )}
              </form>
            </div>

            {/* Time entries */}
            {entries && entries.length > 0 && (
              <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)]">
                <div className="flex items-center justify-between border-b border-[var(--border)] px-5 py-3">
                  <h2 className="flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-[var(--faint)]">
                    Time entries <span className="rounded-full bg-[var(--bg)] px-2 py-0.5 text-xs text-[var(--muted)]">{entries.length}</span>
                  </h2>
                  {(() => {
                    // Totals count only time the PSA actually holds, so this figure reconciles with
                    // the provider's own summary. Rejected entries are called out separately rather
                    // than folded in, where they would silently inflate what the customer is shown.
                    const synced = entries.filter((e) => e.syncStatus === 'Synced');
                    const failed = entries.filter((e) => e.syncStatus !== 'Synced');
                    const total = synced.reduce((a, e) => a + e.hours, 0);
                    const billable = synced.filter((e) => e.billable).reduce((a, e) => a + e.hours, 0);
                    return (
                      <span className="text-xs text-[var(--muted)]">
                        Total: <strong className="text-[var(--fg)]">{fmtDuration(total)}</strong>{' '}
                        ({total.toFixed(4)} h){' · '}billable {fmtDuration(billable)}
                        {failed.length > 0 && (
                          <span className="text-red-600 dark:text-red-400">
                            {' · '}{failed.length} not recorded ({fmtDuration(failed.reduce((a, e) => a + e.hours, 0))})
                          </span>
                        )}
                      </span>
                    );
                  })()}
                </div>
                <ul className="divide-y divide-[var(--border)]">
                  {entries.map((e) => (
                    <li key={e.externalId} className="px-5 py-3">
                      {editEntry?.id === e.externalId ? (
                        <div className="flex flex-wrap items-center gap-2">
                          <input type="number" step="0.25" min="0" value={editEntry.hours}
                            onChange={(ev) => setEditEntry({ ...editEntry, hours: ev.target.value })}
                            className="w-20 rounded-md border border-brand bg-[var(--bg)] px-2 py-1 text-sm outline-none" />
                          <input value={editEntry.notes} onChange={(ev) => setEditEntry({ ...editEntry, notes: ev.target.value })}
                            placeholder="Notes" className="min-w-40 flex-1 rounded-md border border-[var(--border)] bg-[var(--bg)] px-2 py-1 text-sm outline-none focus:border-brand" />
                          <button onClick={() => { const h = parseFloat(editEntry.hours); if (h > 0) updEntry.mutate({ entryId: e.externalId, hours: h, notes: editEntry.notes }); }}
                            disabled={updEntry.isPending} className="rounded-md border border-[var(--border)] p-1.5 text-green-600 hover:bg-[var(--bg)]"><Check size={15} /></button>
                          <button onClick={() => setEditEntry(null)} className="rounded-md border border-[var(--border)] p-1.5 text-[var(--muted)] hover:bg-[var(--bg)]"><X size={15} /></button>
                        </div>
                      ) : (
                        <div>
                          <div className="flex items-center gap-3">
                            <span className="w-24 shrink-0 tabular-nums">
                              <strong>{fmtDuration(e.hours)}</strong>{' '}
                              <span className="text-xs text-[var(--faint)]">({e.hours.toFixed(4)} h)</span>
                            </span>
                            <span className={`shrink-0 rounded px-1.5 py-0.5 text-[11px] font-medium ${e.billable ? 'bg-green-100 text-green-700 dark:bg-green-950 dark:text-green-300' : 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300'}`}>{billableLabel(e.billableOption, e.billable)}</span>
                            {e.workType && <span className="hidden shrink-0 rounded bg-[var(--bg)] px-1.5 py-0.5 text-[11px] text-[var(--muted)] sm:inline">{e.workType}</span>}
                            <span className="min-w-0 flex-1 truncate text-sm text-[var(--muted)]">{e.notes || '—'}</span>
                            {e.technicianName && <span className="hidden shrink-0 text-xs text-[var(--muted)] md:inline">{e.technicianName}</span>}
                            {/* Which system logged it: a technician's own entry reads differently from
                                one raised through the portal, and only one of them can go wrong here. */}
                            <span className={`hidden shrink-0 rounded px-1.5 py-0.5 text-[11px] font-medium sm:inline ${e.source === 'Portal' ? 'bg-brand/10 text-brand' : 'bg-blue-100 text-blue-700 dark:bg-blue-950 dark:text-blue-300'}`}>
                              {e.source === 'Portal' ? 'Desk Portal' : providerLabel(Number(ticket.provider))}
                            </span>
                            <span className="hidden w-32 shrink-0 text-right text-xs lg:inline">
                              {e.syncStatus === 'Synced'
                                ? <span className="text-[var(--muted)]">synced ({providerAbbrev(Number(ticket.provider))} #{e.externalId})</span>
                                : <span className="font-medium text-red-600 dark:text-red-400">{e.syncStatus.toLowerCase()}</span>}
                            </span>
                            <span className="shrink-0 text-xs text-[var(--faint)]">{fmtDay(e.entryDate)}</span>
                            {/* A rejected entry has no provider counterpart to edit. It can be sent
                                again once the cause is fixed, or discarded — leaving it with no
                                actions at all stranded the work on screen permanently. */}
                            {e.syncStatus === 'Synced' ? (
                              <>
                                <button onClick={() => setEditEntry({ id: e.externalId, hours: e.hours.toString(), notes: e.notes ?? '' })}
                                  aria-label="Edit" className="rounded-md p-1.5 text-[var(--muted)] hover:bg-[var(--bg)] hover:text-brand"><Pencil size={14} /></button>
                                <button onClick={() => { if (window.confirm('Delete this time entry from the PSA?')) delEntry.mutate(e.externalId); }}
                                  disabled={delEntry.isPending} aria-label="Delete" className="rounded-md p-1.5 text-[var(--muted)] hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-950/50"><Trash2 size={14} /></button>
                              </>
                            ) : (
                              <>
                                <button onClick={() => retryEntry.mutate(e.externalId)} disabled={retryEntry.isPending}
                                  aria-label="Send to PSA again" title="Send to the PSA again"
                                  className="rounded-md p-1.5 text-[var(--muted)] hover:bg-[var(--bg)] hover:text-brand disabled:opacity-50"><RefreshCw size={14} /></button>
                                <button onClick={() => { if (window.confirm('Discard this entry? It was never recorded in the PSA.')) delEntry.mutate(e.externalId); }}
                                  disabled={delEntry.isPending} aria-label="Discard" title="Discard"
                                  className="rounded-md p-1.5 text-[var(--muted)] hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-950/50"><Trash2 size={14} /></button>
                              </>
                            )}
                          </div>
                          {e.syncStatus !== 'Synced' && (
                            <p className="mt-1 pl-24 text-xs text-red-600 dark:text-red-400">
                              Not recorded in {providerLabel(Number(ticket.provider))}
                              {e.syncError ? `: ${e.syncError}` : '.'}{' '}
                              {retryEntry.isError
                                ? <span className="text-[var(--muted)]">Still rejected — fix the cause, then send again.</span>
                                : <span className="text-[var(--muted)]">Fix the cause, then send again.</span>}
                            </p>
                          )}
                        </div>
                      )}
                    </li>
                  ))}
                </ul>
              </div>
            )}

            {/* Conversation */}
            <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)]">
              <div className="flex items-center justify-between border-b border-[var(--border)] px-5 py-3">
                <h2 className="flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-[var(--faint)]">
                  Conversation <span className="rounded-full bg-[var(--bg)] px-2 py-0.5 text-xs text-[var(--muted)]">{convo.length}</span>
                </h2>
                <button onClick={() => setOldestFirst((v) => !v)} className="inline-flex items-center gap-1.5 rounded-lg border border-[var(--border)] px-2.5 py-1.5 text-xs font-medium text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)]">
                  <ArrowUpDown size={13} /> {oldestFirst ? 'Oldest first' : 'Newest first'} <ChevronDown size={12} />
                </button>
              </div>

              <div className="space-y-4 px-5 py-4">
                {convo.length === 0 && <p className="rounded-lg border border-dashed border-[var(--border)] p-4 text-sm text-[var(--muted)]">No public replies yet.</p>}
                {convo.map((n) => (
                  <div key={n.id} className="flex gap-3">
                    <span className="relative">
                      <span className="flex h-9 w-9 items-center justify-center rounded-full bg-brand text-xs font-semibold text-brand-fg">{initials(n.authorName)}</span>
                      <span className="absolute -bottom-0.5 -right-0.5 h-2.5 w-2.5 rounded-full border-2 border-[var(--surface)] bg-green-500" />
                    </span>
                    <div className="min-w-0 flex-1">
                      <div className="flex items-center justify-between gap-2">
                        <span className="flex items-center gap-2 text-sm">
                          <span className="font-semibold">{n.authorName}</span>
                          <span className={`rounded px-1.5 py-0.5 text-[11px] font-medium ${n.authoredByClient ? 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300' : 'bg-blue-100 text-blue-700 dark:bg-blue-950 dark:text-blue-300'}`}>{n.authoredByClient ? 'Client' : 'Technician'}</span>
                        </span>
                        <span className="shrink-0 text-xs text-[var(--faint)]">{fmt(n.createdAt, true)}</span>
                      </div>
                      <p className="mt-1 whitespace-pre-line text-sm">{n.body}</p>
                      {filesByNote.get(n.id)?.length ? (
                        <ul className="mt-2 flex flex-wrap gap-2">
                          {filesByNote.get(n.id)!.map((a) => (
                            <li key={a.id}>
                              <AttachmentChip a={a} provider={Number(ticket.provider)} onDownload={download} />
                            </li>
                          ))}
                        </ul>
                      ) : null}
                    </div>
                  </div>
                ))}

                {/* Composer */}
                <form onSubmit={(e) => { e.preventDefault(); if (comment.trim()) addComment.mutate(comment.trim()); }} className="rounded-xl border border-[var(--border)] bg-[var(--bg)]">
                  <div className="flex items-center gap-1 border-b border-[var(--border)] px-3 py-2 text-[var(--muted)]">
                    <button type="button" className="rounded p-1.5 hover:bg-[var(--surface)]"><Bold size={15} /></button>
                    <button type="button" onClick={() => fileRef.current?.click()} className="rounded p-1.5 hover:bg-[var(--surface)]"><Paperclip size={15} /></button>
                    <button type="button" className="rounded p-1.5 hover:bg-[var(--surface)]"><Smile size={15} /></button>
                    <button type="button" className="rounded p-1.5 hover:bg-[var(--surface)]"><Link2 size={15} /></button>
                  </div>
                  <textarea value={comment} onChange={(e) => setComment(e.target.value)} rows={3} maxLength={4000}
                    placeholder="Add a reply…" className="w-full resize-y bg-transparent px-4 py-3 text-sm outline-none" />
                  {pendingFiles.length > 0 && (
                    <ul className="flex flex-wrap gap-2 px-4 pb-2">
                      {pendingFiles.map((f, i) => (
                        <li key={`${f.name}-${i}`} className="inline-flex items-center gap-1.5 rounded-lg border border-[var(--border)] bg-[var(--surface)] px-2 py-1 text-xs">
                          <Paperclip size={12} className="text-[var(--muted)]" />
                          <span className="max-w-40 truncate">{f.name}</span>
                          <span className="text-[var(--faint)]">{fmtSize(f.size)}</span>
                          <button type="button" aria-label={`Remove ${f.name}`}
                            onClick={() => setPendingFiles((prev) => prev.filter((_, j) => j !== i))}
                            className="text-[var(--muted)] hover:text-red-600"><X size={12} /></button>
                        </li>
                      ))}
                    </ul>
                  )}
                  <div className="flex items-center justify-between px-4 pb-3">
                    <span className="text-xs text-[var(--faint)]">{comment.length} / 4000</span>
                    <div className="flex items-center gap-2">
                      <button type="button" onClick={() => fileRef.current?.click()} disabled={uploading}
                        className="inline-flex items-center gap-1.5 rounded-lg border border-[var(--border)] bg-[var(--surface)] px-3 py-2 text-sm font-medium hover:bg-[var(--bg)] disabled:opacity-50">
                        <Paperclip size={15} /> Attach file
                      </button>
                      <button type="submit" disabled={addComment.isPending || !comment.trim()}
                        className="inline-flex items-center gap-2 rounded-lg bg-brand px-4 py-2 text-sm font-medium text-brand-fg hover:opacity-90 disabled:opacity-50">
                        <Send size={15} /> {addComment.isPending ? 'Sending…' : pendingFiles.length > 0 ? `Send reply + ${pendingFiles.length} file${pendingFiles.length > 1 ? 's' : ''}` : 'Send reply'}
                      </button>
                    </div>
                  </div>
                  {addComment.isError && <p className="px-4 pb-3 text-xs text-red-600 dark:text-red-400">Couldn&apos;t send — is the API reachable?</p>}
                </form>
              </div>
            </div>

            {/* Attachments */}
            <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5">
              <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-[var(--faint)]">
                Other files <span className="rounded-full bg-[var(--bg)] px-2 py-0.5 text-xs text-[var(--muted)]">{looseFiles.length}</span>
              </h2>
              <p className="-mt-2 mb-3 text-xs text-[var(--muted)]">
                Files not posted with a reply. Anything attached to a message is shown with it above.
              </p>
              {looseFiles.length > 0 && (
                <ul className="mb-3 space-y-2">
                  {looseFiles.map((a) => {
                    const clean = String(a.scanStatus) === '1' || String(a.scanStatus) === 'Clean';
                    const sourceLabel = providerLabel(Number(ticket.provider));
                    return (
                      <li key={a.id} className="flex items-center gap-2 rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm">
                        <Paperclip size={15} className="text-[var(--muted)]" />
                        <span className="truncate">{a.fileName}</span>
                        {a.fromProvider && (
                          <span title={a.authorName ? `Attached by ${a.authorName}` : undefined}
                            className="shrink-0 rounded bg-[var(--bg)] px-1.5 py-0.5 text-[11px] text-[var(--muted)]">
                            From {sourceLabel}
                          </span>
                        )}
                        {!clean && <span className="rounded bg-red-100 px-1.5 py-0.5 text-xs font-medium text-red-700 dark:bg-red-950 dark:text-red-300">Quarantined</span>}
                        <span className="ml-auto text-xs text-[var(--muted)]">{fmtSize(a.sizeBytes)}</span>
                        {clean && <button onClick={() => download(a.id)} className="rounded p-1 text-[var(--muted)] hover:text-brand"><Download size={15} /></button>}
                      </li>
                    );
                  })}
                </ul>
              )}
              <div
                onDragOver={(e) => { e.preventDefault(); setDragOver(true); }}
                onDragLeave={() => setDragOver(false)}
                onDrop={(e) => { e.preventDefault(); setDragOver(false); const f = e.dataTransfer.files?.[0]; if (f) upload(f); }}
                onClick={() => fileRef.current?.click()}
                className={`flex cursor-pointer flex-col items-center rounded-xl border border-dashed px-6 py-8 text-center transition-colors ${dragOver ? 'border-brand bg-brand/5' : 'border-[var(--border)] hover:bg-[var(--bg)]'}`}>
                <Paperclip size={20} className="mb-2 text-[var(--faint)]" />
                <span className="text-sm font-medium">{uploading ? 'Uploading…' : 'Attach a file'}</span>
                <span className="text-xs text-[var(--muted)]">or drag and drop files here</span>
              </div>
              <p className="mt-2 text-xs text-[var(--faint)]">Files are scanned for malware; executables are blocked. Max 25 MB per file.</p>
            </div>

            <input ref={fileRef} type="file" multiple className="hidden" disabled={uploading}
              onChange={(e) => {
                const chosen = Array.from(e.target.files ?? []);
                // Staged, not sent: a file picked in the composer belongs to the reply being written.
                if (chosen.length) setPendingFiles((prev) => [...prev, ...chosen]);
                e.target.value = '';
              }} />
          </>
        );
      })()}
    </div>
  );
}

function Meta({ label, value, href }: { label: string; value: string; href?: string | null }) {
  return (
    <div>
      <dt className="text-[10px] uppercase tracking-wide text-[var(--faint)]">{label}</dt>
      <dd className="mt-0.5 truncate font-medium" title={value}>
        {href ? (
          // Opens the same record in the PSA so a note or a time entry can be checked at source.
          // noreferrer as well as noopener: the PSA has no need to know where the click came from.
          <a
            href={href}
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center gap-1 text-brand hover:underline"
          >
            {value}
            <ExternalLink size={12} aria-hidden="true" />
            <span className="sr-only">— open in the PSA (opens in a new tab)</span>
          </a>
        ) : (
          value
        )}
      </dd>
    </div>
  );
}
