'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Clock, Save, CheckCircle2, AlertTriangle } from 'lucide-react';
import { api } from '@/lib/api';
import { CpHeader, AccessError, Field } from '../_ui';

type Day = { day: string; open: boolean; start: string; end: string };
const DAYS = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
const defaultSchedule = (): Day[] =>
  DAYS.map((d) => ({ day: d, open: d !== 'Sat' && d !== 'Sun', start: '09:00', end: '17:00' }));

function parseSchedule(json: string): Day[] {
  try {
    const arr = JSON.parse(json) as Partial<Day>[];
    if (!Array.isArray(arr) || arr.length === 0) return defaultSchedule();
    return DAYS.map((d) => {
      const m = arr.find((x) => x.day === d);
      return { day: d, open: m?.open ?? false, start: m?.start ?? '09:00', end: m?.end ?? '17:00' };
    });
  } catch { return defaultSchedule(); }
}

export default function BusinessHoursPage() {
  const qc = useQueryClient();
  const { data, isLoading, error } = useQuery({ queryKey: ['cp-business-hours'], queryFn: api.cpBusinessHours, retry: false });
  const [tz, setTz] = useState('');
  const [notes, setNotes] = useState('');
  const [schedule, setSchedule] = useState<Day[]>(defaultSchedule());
  const [dirty, setDirty] = useState(false);

  useEffect(() => {
    if (data && !dirty) {
      setTz(data.timeZone ?? '');
      setNotes(data.notes ?? '');
      setSchedule(parseSchedule(data.scheduleJson));
    }
  }, [data, dirty]);

  const save = useMutation({
    mutationFn: () => api.cpSaveBusinessHours({ timeZone: tz || null, scheduleJson: JSON.stringify(schedule), notes: notes || null }),
    onSuccess: () => { setDirty(false); qc.invalidateQueries({ queryKey: ['cp-business-hours'] }); },
  });

  const setDay = (i: number, patch: Partial<Day>) => {
    setSchedule((s) => s.map((d, idx) => (idx === i ? { ...d, ...patch } : d)));
    setDirty(true);
  };

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <CpHeader icon={Clock} title="Business Hours" subtitle="Your operating hours and time zone, so technicians know when your team is available." />

      {error ? <AccessError label="Business Hours" /> : isLoading ? (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-8 text-center text-sm text-[var(--muted)]">Loading…</div>
      ) : (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5">
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <Field label="Time zone" value={tz} onChange={(v) => { setTz(v); setDirty(true); }} placeholder="America/New_York" />
          </div>

          <div className="mt-4 overflow-hidden rounded-lg border border-[var(--border)]">
            {schedule.map((d, i) => (
              <div key={d.day} className="flex items-center gap-3 border-b border-[var(--border)] px-4 py-2.5 last:border-0">
                <label className="flex w-28 items-center gap-2 text-sm font-medium">
                  <input type="checkbox" checked={d.open} onChange={(e) => setDay(i, { open: e.target.checked })} className="h-4 w-4 accent-brand" />
                  {d.day}
                </label>
                {d.open ? (
                  <div className="flex items-center gap-2 text-sm">
                    <input type="time" value={d.start} onChange={(e) => setDay(i, { start: e.target.value })}
                      className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-2 py-1.5 text-sm outline-none focus:border-brand" />
                    <span className="text-[var(--muted)]">to</span>
                    <input type="time" value={d.end} onChange={(e) => setDay(i, { end: e.target.value })}
                      className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-2 py-1.5 text-sm outline-none focus:border-brand" />
                  </div>
                ) : <span className="text-sm text-[var(--faint)]">Closed</span>}
              </div>
            ))}
          </div>

          <label className="mt-4 block text-xs font-medium text-[var(--muted)]">
            Notes
            <textarea value={notes} onChange={(e) => { setNotes(e.target.value); setDirty(true); }} rows={2}
              placeholder="e.g. After-hours emergencies: call the on-call line."
              className="mt-1 w-full resize-y rounded-lg border border-[var(--border)] bg-[var(--bg)] p-3 text-sm outline-none focus:border-brand" />
          </label>

          <div className="mt-4 flex items-center justify-between gap-3">
            <div className="text-xs">
              {save.isError && <span className="inline-flex items-center gap-1 text-red-600 dark:text-red-400"><AlertTriangle size={13} /> {(save.error as Error)?.message ?? 'Save failed'}</span>}
              {save.isSuccess && !dirty && <span className="inline-flex items-center gap-1 text-green-600 dark:text-green-400"><CheckCircle2 size={13} /> Saved</span>}
              {dirty && !save.isPending && <span className="text-[var(--faint)]">Unsaved changes</span>}
            </div>
            <button onClick={() => save.mutate()} disabled={!dirty || save.isPending}
              className="inline-flex items-center gap-2 rounded-lg bg-brand px-4 py-2 text-sm font-medium text-brand-fg hover:opacity-90 disabled:opacity-40">
              <Save size={15} /> {save.isPending ? 'Saving…' : 'Save'}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
