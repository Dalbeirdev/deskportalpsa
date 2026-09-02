'use client';

import { useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import Link from 'next/link';
import {
  Sparkles, AlignLeft, PenLine, Wand2, Target, AlertCircle, Search, Copy, Check, ArrowRight, ArrowUp,
} from 'lucide-react';
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

export function AssistantRail({ ticketId, draft, onUseDraft, canConfigure = false }: {
  ticketId: string;
  /** The technician's current composer text — what "Improve this draft" works on. */
  draft: string;
  /** Puts drafted text into the composer. The person still sends it. */
  onUseDraft: (text: string) => void;
  /** Whether this person can actually switch the assistant on (connections.manage). */
  canConfigure?: boolean;
}) {
  const [active, setActive] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);
  const [question, setQuestion] = useState('');
  /** The question that produced the answer on screen, so the reader can see what was asked. */
  const [asked, setAsked] = useState<string | null>(null);

  const { data: availability } = useQuery({
    queryKey: ['assistant-availability'],
    queryFn: api.assistantAvailability,
    retry: false,
    staleTime: 5 * 60_000,
  });

  const ask = useMutation({
    mutationFn: ({ action, q }: { action: string; q?: string }) =>
      api.assistantAsk(ticketId, action, action === 'ImproveDraft' ? draft : undefined, q),
  });

  const run = (action: string, q?: string) => {
    setActive(action);
    setCopied(false);
    setAsked(action === 'Ask' ? (q ?? null) : null);
    ask.mutate({ action, q });
  };

  const submitQuestion = () => {
    const q = question.trim();
    if (!q || ask.isPending) return;
    run('Ask', q);
    setQuestion('');
  };

  // Not switched on. For a technician the panel stays absent — a dead control they cannot fix is
  // worse than nothing. For someone who CAN switch it on, silence was the problem: a feature that
  // renders nothing is indistinguishable from a feature that failed to deploy, and the API already
  // says exactly what is missing, so say it and link to the page that fixes it.
  if (!availability?.enabled) {
    if (!canConfigure) return null;
    return (
      <aside className="rounded-xl border border-dashed border-indigo-200 bg-[var(--surface)] p-4 dark:border-indigo-900/60">
        <div className="flex items-center gap-2">
          <Sparkles size={15} className="text-indigo-600 dark:text-indigo-300" />
          <h2 className="text-sm font-semibold text-indigo-700 dark:text-indigo-300">Assistant</h2>
          <span className="ml-auto rounded-full border border-[var(--border)] px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-[var(--faint)]">
            Not set up
          </span>
        </div>
        <p className="mt-2 text-[13px] leading-relaxed text-[var(--muted)]">
          {availability?.reason ?? 'The assistant is switched off for this organization.'}
        </p>
        <Link href="/dashboard/assistant"
          className="mt-3 inline-flex items-center gap-1.5 rounded-lg border border-indigo-200 px-3 py-1.5 text-xs font-medium text-indigo-700 hover:bg-indigo-50 dark:border-indigo-900/60 dark:text-indigo-300 dark:hover:bg-indigo-950/40">
          Set up the assistant <ArrowRight size={13} />
        </Link>
      </aside>
    );
  }

  const answer = ask.data;

  return (
    // A column that fills its rail rather than a card floating in empty space. The three regions
    // divide the height deliberately: the actions keep their natural size, the answer takes what is
    // left and scrolls inside itself, and the composer stays pinned to the bottom where a person
    // looks for it. Without min-h-0 the answer region refuses to shrink and pushes the composer off.
    <aside className="flex h-full min-h-[32rem] flex-col overflow-hidden rounded-xl border border-indigo-200 bg-[var(--surface)] dark:border-indigo-900/60">
      <div className="flex shrink-0 items-center gap-2 border-b border-indigo-200 bg-indigo-50 px-4 py-3 dark:border-indigo-900/60 dark:bg-indigo-950/40">
        <Sparkles size={15} className="text-indigo-600 dark:text-indigo-300" />
        <h2 className="text-sm font-semibold text-indigo-700 dark:text-indigo-300">Assistant</h2>
        <span className="ml-auto rounded-full border border-indigo-200 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-indigo-600 dark:border-indigo-900/60 dark:text-indigo-300">
          Gemini
        </span>
      </div>

      <div className="flex shrink-0 flex-col gap-1.5 p-3">
        {ACTIONS.map(({ key, icon: Icon, label, needsDraft }) => {
          const blocked = Boolean(needsDraft) && !draft.trim();
          return (
            <button
              key={key}
              type="button"
              disabled={ask.isPending || blocked}
              title={blocked ? 'Start writing a reply first' : undefined}
              onClick={() => run(key)}
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

      {/* The answer region owns the leftover height, so a long answer scrolls here instead of
          stretching the rail past the fold. */}
      <div className="min-h-0 flex-1 overflow-y-auto border-t border-[var(--border)] px-3 py-3">
        {ask.isError && (
          <p className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700 dark:border-red-900/60 dark:bg-red-950/30 dark:text-red-300">
            {(ask.error as Error)?.message ?? 'The assistant could not answer.'}
          </p>
        )}

        {!ask.isError && !answer && !ask.isPending && (
          <p className="text-[13px] leading-relaxed text-[var(--faint)]">
            Pick an action above, or ask your own question about this ticket below.
          </p>
        )}

        {ask.isPending && (
          <p className="text-[13px] text-[var(--muted)]">Thinking…</p>
        )}

        {answer && !ask.isPending && (
          <div className="rounded-lg border border-indigo-200 bg-indigo-50/70 p-3 dark:border-indigo-900/60 dark:bg-indigo-950/30">
            {/* What was asked, above the answer — days later a bare answer with no question is
                a paragraph with no subject. */}
            {asked && (
              <p className="mb-2 border-b border-indigo-200/70 pb-2 text-[11px] italic leading-relaxed text-[var(--muted)] dark:border-indigo-900/60">
                {asked}
              </p>
            )}
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
      </div>

      {/* Ask anything. The six actions cover the common asks; this covers the rest without waiting
          for a release. Enter sends, Shift+Enter breaks the line — the convention every chat box
          already taught the reader. */}
      <div className="shrink-0 border-t border-[var(--border)] p-3">
        <div className="flex items-end gap-2 rounded-xl border border-[var(--border)] bg-[var(--bg)] px-2.5 py-2 focus-within:border-indigo-400">
          <label htmlFor="assistant-question" className="sr-only">Ask about this ticket</label>
          <textarea
            id="assistant-question"
            value={question}
            onChange={(e) => setQuestion(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); submitQuestion(); }
            }}
            rows={2}
            maxLength={2000}
            placeholder="Ask about this ticket…"
            disabled={ask.isPending}
            className="max-h-32 min-h-[2.5rem] w-full resize-none bg-transparent text-[13px] leading-relaxed outline-none placeholder:text-[var(--faint)] disabled:opacity-50"
          />
          <button
            type="button"
            onClick={submitQuestion}
            disabled={ask.isPending || !question.trim()}
            aria-label="Ask"
            title="Ask (Enter)"
            className="mb-0.5 shrink-0 rounded-lg bg-indigo-600 p-1.5 text-white hover:bg-indigo-700 disabled:opacity-40"
          >
            <ArrowUp size={14} />
          </button>
        </div>
      </div>

      <p className="shrink-0 border-t border-[var(--border)] px-4 py-2.5 text-[11px] leading-relaxed text-[var(--faint)]">
        Suggestions only — nothing reaches the client or the PSA until you send it yourself.
      </p>
    </aside>
  );
}
