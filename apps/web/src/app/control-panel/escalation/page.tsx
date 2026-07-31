'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowUpCircle, Plus, Pencil, Trash2, Check, X, Info } from 'lucide-react';
import { api, type EscalationLevel, type EscalationInput } from '@/lib/api';
import { CpHeader, AccessError, Field } from '../_ui';

export default function EscalationPage() {
  const qc = useQueryClient();
  const { data, isLoading, error } = useQuery({ queryKey: ['cp-escalation'], queryFn: api.cpEscalation, retry: false });
  const [draft, setDraft] = useState<EscalationInput | null>(null);

  const save = useMutation({
    mutationFn: (input: EscalationInput) => api.cpSaveEscalation(input),
    onSuccess: () => { setDraft(null); qc.invalidateQueries({ queryKey: ['cp-escalation'] }); },
  });
  const del = useMutation({
    mutationFn: (id: string) => api.cpDeleteEscalation(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['cp-escalation'] }),
  });

  return (
    <div className="mx-auto max-w-4xl space-y-5">
      <CpHeader icon={ArrowUpCircle} title="Escalation Procedures" subtitle="The order technicians should follow when a ticket needs to be escalated — who to contact, and when." />
      <div className="flex items-start gap-2 rounded-lg border border-blue-200 bg-blue-50 px-4 py-3 text-sm text-blue-800 dark:border-blue-900 dark:bg-blue-950/40 dark:text-blue-200">
        <Info size={16} className="mt-0.5 shrink-0" /> <p>Levels are followed in order. Set a clear trigger condition for each so technicians know when to escalate.</p>
      </div>

      {error ? <AccessError label="Escalation Procedures" /> : (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)]">
          <div className="flex items-center justify-between border-b border-[var(--border)] px-5 py-3.5">
            <h2 className="text-sm font-semibold">Escalation path</h2>
            <button onClick={() => setDraft({ level: (data?.length ?? 0) + 1, name: '', contact: '', condition: '' })}
              className="inline-flex items-center gap-1.5 rounded-lg bg-brand px-3 py-1.5 text-sm font-medium text-brand-fg hover:opacity-90">
              <Plus size={15} /> Add level
            </button>
          </div>
          <div className="divide-y divide-[var(--border)]">
            {isLoading && <div className="px-5 py-8 text-center text-sm text-[var(--muted)]">Loading…</div>}
            {data?.length === 0 && !draft && <div className="px-5 py-8 text-center text-sm text-[var(--muted)]">No escalation levels yet.</div>}
            {data?.map((e) => <Row key={e.id} level={e} onSave={(i) => save.mutate(i)} onDelete={() => del.mutate(e.id)} saving={save.isPending} />)}
            {draft && <Editor initial={draft} onCancel={() => setDraft(null)} onSave={(i) => save.mutate(i)} saving={save.isPending} />}
          </div>
        </div>
      )}
    </div>
  );
}

function Row({ level, onSave, onDelete, saving }: { level: EscalationLevel; onSave: (i: EscalationInput) => void; onDelete: () => void; saving: boolean }) {
  const [editing, setEditing] = useState(false);
  if (editing) return <Editor initial={level} onCancel={() => setEditing(false)} onSave={(i) => { onSave(i); setEditing(false); }} saving={saving} />;
  return (
    <div className="flex items-center gap-3 px-5 py-3">
      <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-brand/10 text-xs font-semibold text-brand">{level.level}</span>
      <div className="min-w-0 flex-1">
        <div className="font-medium">{level.name}</div>
        <div className="truncate text-xs text-[var(--muted)]">
          {level.contact || '—'}{level.condition ? ` — when: ${level.condition}` : ''}
        </div>
      </div>
      <button onClick={() => setEditing(true)} aria-label="Edit" className="rounded-md p-1.5 text-[var(--muted)] hover:bg-[var(--bg)] hover:text-brand"><Pencil size={15} /></button>
      <button onClick={() => { if (window.confirm(`Remove level ${level.level}?`)) onDelete(); }} aria-label="Delete" className="rounded-md p-1.5 text-[var(--muted)] hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-950/50"><Trash2 size={15} /></button>
    </div>
  );
}

function Editor({ initial, onCancel, onSave, saving }: { initial: EscalationLevel | EscalationInput; onCancel: () => void; onSave: (i: EscalationInput) => void; saving: boolean }) {
  const [f, setF] = useState<EscalationInput>({
    id: 'id' in initial ? initial.id : undefined,
    level: initial.level, name: initial.name, contact: initial.contact ?? '', condition: initial.condition ?? '',
  });
  return (
    <div className="bg-[var(--bg)]/40 px-5 py-4">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-4">
        <Field label="Level" value={String(f.level)} onChange={(v) => setF({ ...f, level: parseInt(v || '1', 10) || 1 })} type="number" />
        <Field label="Name" value={f.name} onChange={(v) => setF({ ...f, name: v })} placeholder="Tier 1 — Help Desk" className="sm:col-span-3" />
        <Field label="Contact" value={f.contact ?? ''} onChange={(v) => setF({ ...f, contact: v })} placeholder="helpdesk@company.com / +1 555 0100" className="sm:col-span-2" />
        <Field label="When to escalate (condition)" value={f.condition ?? ''} onChange={(v) => setF({ ...f, condition: v })} placeholder="No response in 30 min" className="sm:col-span-2" />
      </div>
      <div className="mt-3 flex items-center justify-end gap-2">
        <button onClick={onCancel} className="inline-flex items-center gap-1.5 rounded-lg border border-[var(--border)] px-3 py-1.5 text-sm text-[var(--muted)] hover:bg-[var(--bg)]"><X size={14} /> Cancel</button>
        <button onClick={() => f.name.trim() && onSave(f)} disabled={!f.name.trim() || saving} className="inline-flex items-center gap-1.5 rounded-lg bg-brand px-3 py-1.5 text-sm font-medium text-brand-fg hover:opacity-90 disabled:opacity-40"><Check size={14} /> Save</button>
      </div>
    </div>
  );
}
