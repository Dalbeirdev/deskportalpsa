'use client';

import { createContext, useCallback, useContext, useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Timer, Play, Pause, RotateCcw, ChevronDown, Check, X } from 'lucide-react';
import { api } from '@/lib/api';

// A single work timer shared across the whole dashboard. It can be "attached" to a ticket so time
// can be logged start-to-finish from the header, without reopening the ticket. State survives
// navigation and reloads via localStorage. Provider-neutral: work-type options come from the
// attached connection's cached field set, so this works for any PSA.
type Target = { ticketId: string; ref: string | null; title: string } | null;
type TimerState = { accumulatedMs: number; startedAt: number | null; target: Target };
type TimerContext = {
  running: boolean;
  seconds: number;
  target: Target;
  start: () => void;
  pause: () => void;
  reset: () => void;
  attach: (target: NonNullable<Target>) => void;
  detach: () => void;
};

const Ctx = createContext<TimerContext | null>(null);
const KEY = 'desk.timer.v2';
const EMPTY: TimerState = { accumulatedMs: 0, startedAt: null, target: null };

export function TimerProvider({ children }: { children: React.ReactNode }) {
  const [state, setState] = useState<TimerState>(EMPTY);
  const [, force] = useState(0);

  useEffect(() => {
    try { const raw = localStorage.getItem(KEY); if (raw) setState({ ...EMPTY, ...JSON.parse(raw) }); } catch { /* ignore */ }
  }, []);
  useEffect(() => {
    try { localStorage.setItem(KEY, JSON.stringify(state)); } catch { /* ignore */ }
  }, [state]);
  useEffect(() => {
    if (state.startedAt === null) return;
    const t = setInterval(() => force((n) => n + 1), 1000);
    return () => clearInterval(t);
  }, [state.startedAt]);

  const seconds = Math.floor((state.accumulatedMs + (state.startedAt ? Date.now() - state.startedAt : 0)) / 1000);
  const start = useCallback(() => setState((s) => (s.startedAt ? s : { ...s, startedAt: Date.now() })), []);
  const pause = useCallback(() => setState((s) => (s.startedAt ? { ...s, accumulatedMs: s.accumulatedMs + (Date.now() - s.startedAt), startedAt: null } : s)), []);
  const reset = useCallback(() => setState((s) => ({ ...s, accumulatedMs: 0, startedAt: null })), []);
  const attach = useCallback((target: NonNullable<Target>) => setState((s) => ({ ...s, target })), []);
  const detach = useCallback(() => setState((s) => ({ ...s, target: null })), []);

  return (
    <Ctx.Provider value={{ running: state.startedAt !== null, seconds, target: state.target, start, pause, reset, attach, detach }}>
      {children}
    </Ctx.Provider>
  );
}

export function useTimer() {
  const c = useContext(Ctx);
  if (!c) throw new Error('useTimer must be used within TimerProvider');
  return c;
}

const clock = (secs: number) => `${String(Math.floor(secs / 60)).padStart(2, '0')}:${String(secs % 60).padStart(2, '0')}`;

export function TimerWidget() {
  const { running, seconds, target, start, pause, reset, detach } = useTimer();
  const qc = useQueryClient();
  const [open, setOpen] = useState(false);
  const [billable, setBillable] = useState(true);
  const [workType, setWorkType] = useState('');
  const [notes, setNotes] = useState('');

  const { data: opts } = useQuery({
    queryKey: ['time-options', target?.ticketId],
    queryFn: () => api.ticketTimeOptions(target!.ticketId),
    enabled: open && !!target, retry: false,
  });

  const log = useMutation({
    mutationFn: () => {
      const hours = Math.max(0.25, Math.round((seconds / 3600) / 0.25) * 0.25);
      return api.logTime(target!.ticketId, {
        hours, billable: billable ? 'Billable' : 'DoNotBill',
        notes: notes || undefined, workType: workType || undefined,
      });
    },
    onSuccess: () => {
      reset(); setNotes('');
      [['time-entries', target?.ticketId], ['ticket', target?.ticketId], ['team'], ['trend']]
        .forEach((k) => qc.invalidateQueries({ queryKey: k }));
      setOpen(false);
    },
  });

  return (
    <div className="relative hidden sm:block">
      <div className={`flex items-center gap-1 rounded-lg border px-2 py-1 ${running ? 'border-brand/40 bg-brand/5' : 'border-[var(--border)] bg-[var(--bg)]'}`}>
        <Timer size={14} className={running ? 'text-brand motion-safe:animate-pulse' : 'text-[var(--muted)]'} />
        {target?.ref && (
          <span className="flex items-center gap-1" title={target.title}>
            <span className="max-w-[6rem] truncate text-xs font-medium">#{target.ref}</span>
            <span className="text-[var(--faint)]">·</span>
          </span>
        )}
        <span className="tabular-nums text-sm font-medium">{clock(seconds)}</span>
        <button onClick={running ? pause : start} aria-label={running ? 'Pause timer' : 'Start timer'}
          className="rounded p-1 text-[var(--muted)] hover:bg-[var(--surface)] hover:text-[var(--fg)]">
          {running ? <Pause size={13} /> : <Play size={13} />}
        </button>
        <button onClick={() => setOpen((o) => !o)} aria-label="Time logging options"
          className="rounded p-1 text-[var(--muted)] hover:bg-[var(--surface)] hover:text-[var(--fg)]">
          <ChevronDown size={13} className={open ? 'rotate-180 transition-transform' : 'transition-transform'} />
        </button>
      </div>

      {open && (
        <>
          <div className="fixed inset-0 z-10" onClick={() => setOpen(false)} />
          <div className="absolute right-0 z-20 mt-2 w-72 rounded-xl border border-[var(--border)] bg-[var(--surface)] p-3 text-sm shadow-lg">
            <div className="mb-2 flex items-center justify-between">
              <span className="font-semibold">Log time</span>
              <span className="tabular-nums text-[var(--muted)]">{clock(seconds)}</span>
            </div>
            {target ? (
              <div className="space-y-2">
                <div className="rounded-lg bg-[var(--bg)] px-2.5 py-1.5 text-xs">
                  <span className="text-[var(--muted)]">Ticket </span>
                  <span className="font-medium">{target.ref ? `#${target.ref}` : '—'}</span>
                  <span className="block truncate text-[var(--muted)]">{target.title}</span>
                </div>
                {opts && opts.workTypes.length > 0 && (
                  <label className="block">
                    <span className="mb-1 block text-xs text-[var(--muted)]">Time type</span>
                    <select value={workType} onChange={(e) => setWorkType(e.target.value)}
                      className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-2 py-1.5 text-sm outline-none focus:border-brand">
                      <option value="">—</option>
                      {opts.workTypes.map((o) => <option key={o.value} value={o.label}>{o.label}</option>)}
                    </select>
                  </label>
                )}
                <label className="flex items-center gap-2 text-sm">
                  <input type="checkbox" checked={billable} onChange={(e) => setBillable(e.target.checked)} className="h-4 w-4 accent-[var(--brand-line,#3b82f6)]" />
                  Billable
                </label>
                <input value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="Notes (optional)"
                  className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-2 py-1.5 text-sm outline-none focus:border-brand" />
                <div className="flex items-center gap-2 pt-1">
                  <button onClick={() => log.mutate()} disabled={log.isPending || seconds < 1}
                    className="inline-flex flex-1 items-center justify-center gap-1.5 rounded-lg bg-brand px-3 py-2 text-sm font-medium text-brand-fg hover:opacity-90 disabled:opacity-50">
                    <Check size={14} /> {log.isPending ? 'Logging…' : `Log ${clock(seconds)}`}
                  </button>
                  <button onClick={reset} aria-label="Reset timer" className="rounded-lg border border-[var(--border)] p-2 text-[var(--muted)] hover:bg-[var(--bg)]"><RotateCcw size={14} /></button>
                </div>
                {log.isError && <p className="text-xs text-red-600 dark:text-red-400">Couldn&apos;t log — is the API reachable?</p>}
                <button onClick={detach} className="flex items-center gap-1 text-xs text-[var(--muted)] hover:text-[var(--fg)]"><X size={12} /> Detach from ticket</button>
              </div>
            ) : (
              <p className="text-xs text-[var(--muted)]">Open a ticket and choose <span className="font-medium text-[var(--fg)]">“Start timer here”</span> to log time against it from anywhere.</p>
            )}
          </div>
        </>
      )}
    </div>
  );
}
