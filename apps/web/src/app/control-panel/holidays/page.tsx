'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarDays, Plus, Trash2, Check, X } from 'lucide-react';
import { api, type HolidayInput } from '@/lib/api';
import { CpHeader, AccessError, Field } from '../_ui';

function fmtDate(iso: string) {
  const d = new Date(iso + 'T00:00:00');
  return isNaN(d.getTime()) ? iso : d.toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric', year: 'numeric' });
}

export default function HolidaysPage() {
  const qc = useQueryClient();
  const { data, isLoading, error } = useQuery({ queryKey: ['cp-holidays'], queryFn: api.cpHolidays, retry: false });
  const [draft, setDraft] = useState<HolidayInput | null>(null);

  const save = useMutation({
    mutationFn: (input: HolidayInput) => api.cpSaveHoliday(input),
    onSuccess: () => { setDraft(null); qc.invalidateQueries({ queryKey: ['cp-holidays'] }); },
  });
  const del = useMutation({
    mutationFn: (id: string) => api.cpDeleteHoliday(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['cp-holidays'] }),
  });

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <CpHeader icon={CalendarDays} title="Holidays" subtitle="Days your organization is closed or on reduced coverage, so technicians plan around them." />

      {error ? <AccessError label="Holidays" /> : (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)]">
          <div className="flex items-center justify-between border-b border-[var(--border)] px-5 py-3.5">
            <h2 className="text-sm font-semibold">Holidays</h2>
            <button onClick={() => setDraft({ date: '', name: '' })}
              className="inline-flex items-center gap-1.5 rounded-lg bg-brand px-3 py-1.5 text-sm font-medium text-brand-fg hover:opacity-90">
              <Plus size={15} /> Add holiday
            </button>
          </div>
          <div className="divide-y divide-[var(--border)]">
            {isLoading && <div className="px-5 py-8 text-center text-sm text-[var(--muted)]">Loading…</div>}
            {data?.length === 0 && !draft && <div className="px-5 py-8 text-center text-sm text-[var(--muted)]">No holidays yet.</div>}
            {data?.map((h) => (
              <div key={h.id} className="flex items-center gap-3 px-5 py-3">
                <CalendarDays size={16} className="shrink-0 text-[var(--muted)]" />
                <span className="w-56 shrink-0 text-sm tabular-nums text-[var(--muted)]">{fmtDate(h.date)}</span>
                <span className="min-w-0 flex-1 truncate font-medium">{h.name}</span>
                <button onClick={() => { if (window.confirm(`Remove ${h.name}?`)) del.mutate(h.id); }} aria-label="Delete" className="rounded-md p-1.5 text-[var(--muted)] hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-950/50"><Trash2 size={15} /></button>
              </div>
            ))}
            {draft && (
              <div className="bg-[var(--bg)]/40 px-5 py-4">
                <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                  <Field label="Date" value={draft.date} onChange={(v) => setDraft({ ...draft, date: v })} type="date" />
                  <Field label="Name" value={draft.name} onChange={(v) => setDraft({ ...draft, name: v })} placeholder="Independence Day" />
                </div>
                <div className="mt-3 flex items-center justify-end gap-2">
                  <button onClick={() => setDraft(null)} className="inline-flex items-center gap-1.5 rounded-lg border border-[var(--border)] px-3 py-1.5 text-sm text-[var(--muted)] hover:bg-[var(--bg)]"><X size={14} /> Cancel</button>
                  <button onClick={() => draft.date && draft.name.trim() && save.mutate(draft)} disabled={!draft.date || !draft.name.trim() || save.isPending}
                    className="inline-flex items-center gap-1.5 rounded-lg bg-brand px-3 py-1.5 text-sm font-medium text-brand-fg hover:opacity-90 disabled:opacity-40"><Check size={14} /> Save</button>
                </div>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
