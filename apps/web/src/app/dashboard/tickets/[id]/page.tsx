'use client';

import { use, useRef, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ArrowLeft, ChevronLeft, ChevronRight, ChevronDown, Pencil, MoreHorizontal, Paperclip,
  Send, Bold, Smile, Link2, ArrowUpDown, Lock, Monitor, Wifi, Mail, KeyRound, Cpu, Ticket,
  Copy, RefreshCw, Download, Clock,
} from 'lucide-react';
import { api } from '@/lib/api';

const STATUS_TONE: Record<string, string> = {
  NEW: 'bg-blue-100 text-blue-700 dark:bg-blue-950 dark:text-blue-300',
  IN_PROGRESS: 'bg-amber-100 text-amber-700 dark:bg-amber-950 dark:text-amber-300',
  WAITING_CUSTOMER: 'bg-violet-100 text-violet-700 dark:bg-violet-950 dark:text-violet-300',
  ON_HOLD: 'bg-orange-100 text-orange-700 dark:bg-orange-950 dark:text-orange-300',
  RESOLVED: 'bg-green-100 text-green-700 dark:bg-green-950 dark:text-green-300',
  CLOSED: 'bg-slate-200 text-slate-600 dark:bg-slate-800 dark:text-slate-300',
};
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

export default function TicketDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const qc = useQueryClient();
  const router = useRouter();
  const [comment, setComment] = useState('');
  const [uploading, setUploading] = useState(false);
  const [dragOver, setDragOver] = useState(false);
  const [oldestFirst, setOldestFirst] = useState(true);
  const [menuOpen, setMenuOpen] = useState(false);
  const [hours, setHours] = useState('');
  const [billable, setBillable] = useState('Billable');
  const [timeNotes, setTimeNotes] = useState('');
  const replyRef = useRef<HTMLTextAreaElement>(null);
  const fileRef = useRef<HTMLInputElement>(null);

  const { data: ticket, isLoading, isError } = useQuery({ queryKey: ['ticket', id], queryFn: () => api.getTicket(id) });
  const { data: list } = useQuery({ queryKey: ['tickets'], queryFn: api.listTickets });

  const addComment = useMutation({
    mutationFn: (body: string) => api.addComment(id, body),
    onSuccess: () => { setComment(''); qc.invalidateQueries({ queryKey: ['ticket', id] }); },
  });

  const logTime = useMutation({
    mutationFn: () => api.logTime(id, { hours: parseFloat(hours), billable, notes: timeNotes || undefined }),
    onSuccess: () => {
      setHours(''); setTimeNotes('');
      ['ticket', 'team', 'trend'].forEach((k) => qc.invalidateQueries({ queryKey: k === 'ticket' ? ['ticket', id] : [k] }));
    },
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
          <button onClick={() => replyRef.current?.focus()} className="inline-flex items-center gap-1.5 rounded-lg border border-[var(--border)] bg-[var(--surface)] px-3 py-1.5 text-sm font-medium hover:bg-[var(--bg)]">
            <Pencil size={14} /> Edit ticket
          </button>
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
                    <p className="text-sm text-[var(--muted)]">{ticket.description ?? 'No description provided.'}</p>
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  <span className={`rounded-md px-2.5 py-1 text-xs font-semibold ${STATUS_TONE[ticket.portalStatus] ?? STATUS_TONE.NEW}`}>{ticket.portalStatus.replace(/_/g, ' ')}</span>
                  <span className={`inline-flex items-center gap-1 rounded-md px-2.5 py-1 text-xs font-semibold ${PRIORITY_TONE[ticket.portalPriority.toUpperCase()] ?? PRIORITY_TONE.NORMAL}`}>{ticket.portalPriority.toUpperCase()} <ChevronDown size={12} /></span>
                </div>
              </div>
              <dl className="mt-5 grid grid-cols-2 gap-x-6 gap-y-3 border-t border-[var(--border)] pt-4 text-sm sm:grid-cols-3 lg:grid-cols-6">
                <Meta label="Reference" value={ticket.externalTicketId ?? '—'} />
                <Meta label="Queue / Board" value={ticket.queueOrBoard ?? '—'} />
                <Meta label="Category" value={ticket.portalCategory ?? '—'} />
                <Meta label="Customer" value={ticket.customerName ?? '—'} />
                <Meta label="Opened" value={fmt(ticket.createdAt)} />
                <Meta label="Updated" value={fmt(ticket.updatedAt)} />
              </dl>
            </div>

            {/* Log time */}
            <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5">
              <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-[var(--faint)]"><Clock size={14} /> Log time</h2>
              <form onSubmit={(e) => { e.preventDefault(); if (parseFloat(hours) > 0) logTime.mutate(); }} className="flex flex-wrap items-end gap-3">
                <label className="block">
                  <span className="mb-1 block text-xs text-[var(--muted)]">Hours</span>
                  <input type="number" step="0.25" min="0" value={hours} onChange={(e) => setHours(e.target.value)} placeholder="0.5"
                    className="w-24 rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm outline-none focus:border-brand" />
                </label>
                <label className="block">
                  <span className="mb-1 block text-xs text-[var(--muted)]">Billable</span>
                  <select value={billable} onChange={(e) => setBillable(e.target.value)}
                    className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm outline-none focus:border-brand">
                    <option value="Billable">Billable</option>
                    <option value="DoNotBill">Do not bill</option>
                    <option value="NoCharge">No charge</option>
                  </select>
                </label>
                <label className="block min-w-48 flex-1">
                  <span className="mb-1 block text-xs text-[var(--muted)]">Notes</span>
                  <input value={timeNotes} onChange={(e) => setTimeNotes(e.target.value)} placeholder="What did you work on?"
                    className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm outline-none focus:border-brand" />
                </label>
                <button type="submit" disabled={logTime.isPending || !(parseFloat(hours) > 0)}
                  className="inline-flex items-center gap-2 rounded-lg bg-brand px-4 py-2 text-sm font-medium text-brand-fg hover:opacity-90 disabled:opacity-50">
                  <Clock size={15} /> {logTime.isPending ? 'Logging…' : 'Log time'}
                </button>
              </form>
              {logTime.isSuccess && logTime.data && (
                <p className="mt-2 text-xs font-medium text-green-600 dark:text-green-400">
                  Logged to the PSA · ticket total {logTime.data.timeWorkedHours}h ({logTime.data.billableHours}h billable).
                </p>
              )}
              {logTime.isError && (
                <p className="mt-2 text-xs text-red-600 dark:text-red-400">Couldn&apos;t log time — the PSA rejected it or the connection is unreachable.</p>
              )}
              <p className="mt-2 text-xs text-[var(--faint)]">Posts a time entry to the PSA against this ticket. Work type &amp; role arrive in a later update.</p>
            </div>

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
                      <p className="mt-1 text-sm">{n.body}</p>
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
                  <textarea ref={replyRef} value={comment} onChange={(e) => setComment(e.target.value)} rows={3} maxLength={4000}
                    placeholder="Add a reply…" className="w-full resize-y bg-transparent px-4 py-3 text-sm outline-none" />
                  <div className="flex items-center justify-between px-4 pb-3">
                    <span className="text-xs text-[var(--faint)]">{comment.length} / 4000</span>
                    <div className="flex items-center gap-2">
                      <button type="button" onClick={() => fileRef.current?.click()} disabled={uploading}
                        className="inline-flex items-center gap-1.5 rounded-lg border border-[var(--border)] bg-[var(--surface)] px-3 py-2 text-sm font-medium hover:bg-[var(--bg)] disabled:opacity-50">
                        <Paperclip size={15} /> {uploading ? 'Uploading…' : 'Attach file'}
                      </button>
                      <button type="submit" disabled={addComment.isPending || !comment.trim()}
                        className="inline-flex items-center gap-2 rounded-lg bg-brand px-4 py-2 text-sm font-medium text-brand-fg hover:opacity-90 disabled:opacity-50">
                        <Send size={15} /> {addComment.isPending ? 'Sending…' : 'Send reply'}
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
                Attachments <span className="rounded-full bg-[var(--bg)] px-2 py-0.5 text-xs text-[var(--muted)]">{ticket.attachments.length}</span>
              </h2>
              {ticket.attachments.length > 0 && (
                <ul className="mb-3 space-y-2">
                  {ticket.attachments.map((a) => {
                    const clean = String(a.scanStatus) === '1' || String(a.scanStatus) === 'Clean';
                    return (
                      <li key={a.id} className="flex items-center gap-2 rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm">
                        <Paperclip size={15} className="text-[var(--muted)]" />
                        <span className="truncate">{a.fileName}</span>
                        {!clean && <span className="rounded bg-red-100 px-1.5 py-0.5 text-xs font-medium text-red-700 dark:bg-red-950 dark:text-red-300">Quarantined</span>}
                        <span className="ml-auto text-xs text-[var(--muted)]">{Math.round(a.sizeBytes / 1024)} KB</span>
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

            <input ref={fileRef} type="file" className="hidden" disabled={uploading}
              onChange={(e) => { const f = e.target.files?.[0]; if (f) upload(f); e.target.value = ''; }} />
          </>
        );
      })()}
    </div>
  );
}

function Meta({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-[10px] uppercase tracking-wide text-[var(--faint)]">{label}</dt>
      <dd className="mt-0.5 truncate font-medium" title={value}>{value}</dd>
    </div>
  );
}
