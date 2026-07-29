'use client';

import { use, useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, Paperclip, Send } from 'lucide-react';
import { api } from '@/lib/api';
import { StatusBadge, PriorityBadge } from '@/components/badges';

export default function TicketDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const qc = useQueryClient();
  const [comment, setComment] = useState('');
  const [uploading, setUploading] = useState(false);

  async function downloadAttachment(ticketId: string, attachmentId: string) {
    try {
      const { url } = await api.attachmentDownloadUrl(ticketId, attachmentId);
      window.open(url, '_blank', 'noopener');
    } catch {
      /* preview runs without a backend */
    }
  }

  const { data: ticket, isLoading, isError } = useQuery({
    queryKey: ['ticket', id],
    queryFn: () => api.getTicket(id),
  });

  const addComment = useMutation({
    mutationFn: (body: string) => api.addComment(id, body),
    onSuccess: () => {
      setComment('');
      qc.invalidateQueries({ queryKey: ['ticket', id] });
    },
  });

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <Link href="/dashboard/tickets" className="inline-flex items-center gap-1.5 text-sm text-[var(--muted)] hover:text-[var(--fg)]">
        <ArrowLeft size={15} /> Back to tickets
      </Link>

      {isLoading && <div className="h-40 animate-pulse rounded-xl border border-[var(--border)] bg-[var(--surface)]" />}
      {isError && (
        <div className="rounded-xl border border-dashed border-[var(--border)] p-8 text-center text-sm text-[var(--muted)]">
          Couldn&apos;t load this ticket. This preview runs without a live backend.
        </div>
      )}

      {ticket && (
        <>
          <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-6">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <h1 className="text-lg font-semibold">{ticket.title}</h1>
              <div className="flex gap-2">
                <StatusBadge status={ticket.portalStatus} />
                <PriorityBadge priority={ticket.portalPriority} />
              </div>
            </div>
            {ticket.description && <p className="mt-3 text-sm text-[var(--muted)]">{ticket.description}</p>}
            <dl className="mt-4 grid grid-cols-2 gap-x-6 gap-y-2 text-sm sm:grid-cols-4">
              <Meta label="Reference" value={ticket.externalTicketId ?? '—'} />
              <Meta label="Queue" value={ticket.queueOrBoard ?? '—'} />
              <Meta label="Category" value={ticket.portalCategory ?? '—'} />
              <Meta label="Opened" value={new Date(ticket.createdAt).toLocaleDateString()} />
            </dl>
          </div>

          <section>
            <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-[var(--faint)]">Conversation</h2>
            <div className="space-y-3">
              {ticket.conversation.length === 0 && (
                <p className="rounded-lg border border-dashed border-[var(--border)] p-4 text-sm text-[var(--muted)]">
                  No public replies yet.
                </p>
              )}
              {ticket.conversation.map((n) => (
                <div key={n.id} className="rounded-lg border border-[var(--border)] bg-[var(--surface)] p-4">
                  <div className="mb-1 flex items-center justify-between text-xs text-[var(--muted)]">
                    <span className="font-medium text-[var(--fg)]">
                      {n.authorName} {n.authoredByClient && <span className="text-[var(--faint)]">(you)</span>}
                    </span>
                    <span>{new Date(n.createdAt).toLocaleString()}</span>
                  </div>
                  <p className="text-sm">{n.body}</p>
                </div>
              ))}
            </div>

            <form
              className="mt-4"
              onSubmit={(e) => {
                e.preventDefault();
                if (comment.trim()) addComment.mutate(comment.trim());
              }}
            >
              <textarea
                value={comment}
                onChange={(e) => setComment(e.target.value)}
                rows={3}
                placeholder="Add a reply…"
                className="w-full rounded-lg border border-[var(--border)] bg-[var(--surface)] p-3 text-sm outline-none focus:border-brand"
              />
              <div className="mt-2 flex justify-end">
                <button
                  type="submit"
                  disabled={addComment.isPending || !comment.trim()}
                  className="inline-flex items-center gap-2 rounded-lg bg-brand px-3.5 py-2 text-sm font-medium text-brand-fg hover:opacity-90 disabled:opacity-50"
                >
                  <Send size={15} /> {addComment.isPending ? 'Sending…' : 'Send reply'}
                </button>
              </div>
            </form>
          </section>

          <section>
            <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-[var(--faint)]">Attachments</h2>
            <ul className="space-y-2">
              {ticket.attachments.map((a) => {
                const clean = String(a.scanStatus) === '1' || String(a.scanStatus) === 'Clean';
                return (
                  <li key={a.id} className="flex items-center gap-2 rounded-lg border border-[var(--border)] bg-[var(--surface)] px-3 py-2 text-sm">
                    <Paperclip size={15} className="text-[var(--muted)]" />
                    {clean ? (
                      <button onClick={() => downloadAttachment(id, a.id)} className="hover:underline">{a.fileName}</button>
                    ) : (
                      <span>{a.fileName}</span>
                    )}
                    {!clean && (
                      <span className="rounded bg-red-100 px-1.5 py-0.5 text-xs font-medium text-red-700 dark:bg-red-950 dark:text-red-300">Quarantined</span>
                    )}
                    <span className="ml-auto text-xs text-[var(--muted)]">{Math.round(a.sizeBytes / 1024)} KB</span>
                  </li>
                );
              })}
              {ticket.attachments.length === 0 && (
                <li className="text-sm text-[var(--muted)]">No attachments.</li>
              )}
            </ul>

            <label className="mt-3 inline-flex cursor-pointer items-center gap-2 rounded-lg border border-[var(--border)] px-3 py-2 text-sm font-medium hover:bg-[var(--bg)]">
              <Paperclip size={15} /> {uploading ? 'Uploading…' : 'Attach a file'}
              <input
                type="file"
                className="hidden"
                disabled={uploading}
                onChange={async (e) => {
                  const file = e.target.files?.[0];
                  if (!file) return;
                  setUploading(true);
                  try {
                    await api.uploadAttachment(id, file);
                    qc.invalidateQueries({ queryKey: ['ticket', id] });
                  } catch {
                    /* preview runs without a backend */
                  } finally {
                    setUploading(false);
                    e.target.value = '';
                  }
                }}
              />
            </label>
            <p className="mt-1.5 text-xs text-[var(--faint)]">
              Files are scanned for malware; executables are blocked. Max 25 MB.
            </p>
          </section>
        </>
      )}
    </div>
  );
}

function Meta({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wide text-[var(--faint)]">{label}</dt>
      <dd className="mt-0.5">{value}</dd>
    </div>
  );
}
