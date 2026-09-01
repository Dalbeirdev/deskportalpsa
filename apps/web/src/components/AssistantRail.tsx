'use client';

import { useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Sparkles, AlignLeft, PenLine, Wand2, Target, AlertCircle, Search, Copy, Check } from 'lucide-react';
import { api } from '@/lib/api';

/**
 * The ticket assistant.
 *
 * Kept in its own indigo, never the portal's green: a suggestion must never be mistaken for a fact
 * the PSA reported. Nothing here writes anywhere — draft output goes into the composer for a person
 * to read, edit and send.
 */
type Action = { key: string; icon: React.ElementType; label: string; needsDraft?: boolean };

const ACTIONS: Action[] = [
  { key: 'Summarise', icon: AlignLeft, label: 'Summarise this ticket' },
  { key: 'DraftReply', icon: PenLine, label: 'Draft a reply' },
  // Improving nothing is meaningless, so this one waits for the technician to start writing.
  { key: 'ImproveDraft', icon: Wand2, label: 'Improve this draft', needsDraft: true },
  { key: 'NextSteps', icon: Target, label: 'Suggest next steps' },
  { key: 'ExplainError', icon: AlertCircle, label: 'Explain this PSA error' },
  { key: 'SimilarTickets', icon: Search, label: 'Find similar tickets' },
];

export function AssistantRail({ ticketId, draft, onUseDraft }: {
  ticketId: string;
  /** The technician's current composer text — what "Improve this draft" works on. */
  draft: string;
  /** Puts drafted text into the composer. The person still sends it. */
  onUseDraft: (text: string) => void;
}) {
  const [active, setActive] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);

  const { data: availability } = useQuery({
    queryKey: ['assistant-availability'],
    queryFn: api.assistantAvailability,
    retry: false,
    staleTime: 5 * 60_000,
  });

  const ask = useMutation({
    mutationFn: (action: string) => api.assistantAsk(ticketId, action, action === 'ImproveDraft' ? draft : undefined),
  });

  // Absent rather than broken: an organization that has not switched this on should not be shown a
  // panel it cannot use.
  if (!availability?.enabled) return null;

  const answer = ask.data;

  return (
    <aside className="rounded-xl border border-indigo-200 bg-[var(--surface)] dark:border-indigo-900/60">
      <div className="flex items-center gap-2 rounded-t-xl border-b border-indigo-200 bg-indigo-50 px-4 py-3 dark:border-indigo-900/60 dark:bg-indigo-950/40">
        <Sparkles size={15} className="text-indigo-600 dark:text-indigo-300" />
        <h2 className="text-sm font-semibold text-indigo-700 dark:text-indigo-300">Assistant</h2>
        <span className="ml-auto rounded-full border border-indigo-200 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-indigo-600 dark:border-indigo-900/60 dark:text-indigo-300">
          Gemini
        </span>
      </div>

      <div className="flex flex-col gap-1.5 p-3">
        {ACTIONS.map(({ key, icon: Icon, label, needsDraft }) => {
          const blocked = Boolean(needsDraft) && !draft.trim();
          return (
            <button
              key={key}
              type="button"
              disabled={ask.isPending || blocked}
              title={blocked ? 'Start writing a reply first' : undefined}
              onClick={() => { setActive(key); setCopied(false); ask.mutate(key); }}
              className={`flex items-center gap-2.5 rounded-lg border px-3 py-2 text-left text-[13px] font-medium transition-colors disabled:opacity-45 ${
                active === key && ask.isPending
                  ? 'border-indigo-400 bg-indigo-50 dark:bg-indigo-950/40'
                  : 'border-[var(--border)] hover:border-indigo-400 hover:bg-indigo-50 dark:hover:bg-indigo-950/40'
              }`}
            >
              <Icon size={15} className="shrink-0 text-indigo-600 dark:text-indigo-300" />
              {active === key && ask.isPending ? 'Thinking…' : label}
            </button>
          );
        })}
      </div>

      {ask.isError && (
        <p className="mx-3 mb-3 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700 dark:border-red-900/60 dark:bg-red-950/30 dark:text-red-300">
          {(ask.error as Error)?.message ?? 'The assistant could not answer.'}
        </p>
      )}

      {answer && !ask.isPending && (
        <div className="mx-3 mb-3 rounded-lg border border-indigo-200 bg-indigo-50/70 p-3 dark:border-indigo-900/60 dark:bg-indigo-950/30">
          <p className="whitespace-pre-wrap text-[13px] leading-relaxed text-[var(--fg)]">{answer.text}</p>
          <div className="mt-2.5 flex flex-wrap gap-2">
            {answer.isDraft && (
              <button
                type="button"
                onClick={() => onUseDraft(answer.text)}
                className="rounded-lg bg-indigo-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-indigo-700"
              >
                Use in reply
              </button>
            )}
            <button
              type="button"
              onClick={() => { navigator.clipboard?.writeText(answer.text); setCopied(true); }}
              className="inline-flex items-center gap-1.5 rounded-lg border border-[var(--border)] px-3 py-1.5 text-xs font-medium hover:bg-[var(--bg)]"
            >
              {copied ? <><Check size={12} /> Copied</> : <><Copy size={12} /> Copy</>}
            </button>
          </div>
        </div>
      )}

      <p className="border-t border-[var(--border)] px-4 py-2.5 text-[11px] leading-relaxed text-[var(--faint)]">
        Suggestions only — nothing reaches the client or the PSA until you send it yourself.
      </p>
    </aside>
  );
}
