'use client';

import { createContext, useCallback, useContext, useEffect, useState } from 'react';
import { Timer, Play, Pause, RotateCcw } from 'lucide-react';

// A single work timer shared across the whole dashboard. State survives navigation and reloads via
// localStorage, so you can start it, move between tickets, and apply the elapsed time when logging.
type TimerState = { accumulatedMs: number; startedAt: number | null };
type TimerContext = { running: boolean; seconds: number; start: () => void; pause: () => void; reset: () => void };

const Ctx = createContext<TimerContext | null>(null);
const KEY = 'desk.timer.v1';

export function TimerProvider({ children }: { children: React.ReactNode }) {
  const [state, setState] = useState<TimerState>({ accumulatedMs: 0, startedAt: null });
  const [, force] = useState(0); // re-render each second while running

  useEffect(() => {
    try { const raw = localStorage.getItem(KEY); if (raw) setState(JSON.parse(raw)); } catch { /* ignore */ }
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
  const pause = useCallback(() => setState((s) => (s.startedAt ? { accumulatedMs: s.accumulatedMs + (Date.now() - s.startedAt), startedAt: null } : s)), []);
  const reset = useCallback(() => setState({ accumulatedMs: 0, startedAt: null }), []);

  return <Ctx.Provider value={{ running: state.startedAt !== null, seconds, start, pause, reset }}>{children}</Ctx.Provider>;
}

export function useTimer() {
  const c = useContext(Ctx);
  if (!c) throw new Error('useTimer must be used within TimerProvider');
  return c;
}

export function TimerWidget() {
  const { running, seconds, start, pause, reset } = useTimer();
  const mm = String(Math.floor(seconds / 60)).padStart(2, '0');
  const ss = String(seconds % 60).padStart(2, '0');
  return (
    <div className={`hidden items-center gap-1 rounded-lg border px-2 py-1 sm:flex ${running ? 'border-brand/40 bg-brand/5' : 'border-[var(--border)] bg-[var(--bg)]'}`}
      title="Work timer — runs across the whole portal">
      <Timer size={14} className={running ? 'text-brand' : 'text-[var(--muted)]'} />
      <span className="tabular-nums text-sm font-medium">{mm}:{ss}</span>
      <button onClick={running ? pause : start} aria-label={running ? 'Pause timer' : 'Start timer'}
        className="rounded p-1 text-[var(--muted)] hover:bg-[var(--surface)] hover:text-[var(--fg)]">
        {running ? <Pause size={13} /> : <Play size={13} />}
      </button>
      <button onClick={reset} aria-label="Reset timer"
        className="rounded p-1 text-[var(--muted)] hover:bg-[var(--surface)] hover:text-[var(--fg)]">
        <RotateCcw size={13} />
      </button>
    </div>
  );
}
