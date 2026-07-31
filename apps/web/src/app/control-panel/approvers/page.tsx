'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { UserCheck, Plus, Pencil, Trash2, Check, X, Info } from 'lucide-react';
import { api, type Approver, type ApproverInput } from '@/lib/api';
import { CpHeader, AccessError, Field } from '../_ui';

const empty: ApproverInput = { name: '', email: '', phone: '', scope: '', sortOrder: 0 };

export default function ApproversPage() {
  const qc = useQueryClient();
  const { data, isLoading, error } = useQuery({ queryKey: ['cp-approvers'], queryFn: api.cpApprovers, retry: false });
  const [draft, setDraft] = useState<ApproverInput | null>(null);

  const save = useMutation({
    mutationFn: (input: ApproverInput) => api.cpSaveApprover(input),
    onSuccess: () => { setDraft(null); qc.invalidateQueries({ queryKey: ['cp-approvers'] }); },
  });
  const del = useMutation({
    mutationFn: (id: string) => api.cpDeleteApprover(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['cp-approvers'] }),
  });

  return (
    <div className="mx-auto max-w-4xl space-y-5">
      <CpHeader icon={UserCheck} title="Approvers" subtitle="People you authorize to approve requests such as new users, access changes, or purchases." />
      <div className="flex items-start gap-2 rounded-lg border border-blue-200 bg-blue-50 px-4 py-3 text-sm text-blue-800 dark:border-blue-900 dark:bg-blue-950/40 dark:text-blue-200">
        <Info size={16} className="mt-0.5 shrink-0" /> <p>Technicians check this list before actioning approval-required tickets.</p>
      </div>

      {error ? <AccessError label="Approvers" /> : (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)]">
          <div className="flex items-center justify-between border-b border-[var(--border)] px-5 py-3.5">
            <h2 className="text-sm font-semibold">Approvers</h2>
            <button onClick={() => setDraft({ ...empty, sortOrder: (data?.length ?? 0) + 1 })}
              className="inline-flex items-center gap-1.5 rounded-lg bg-brand px-3 py-1.5 text-sm font-medium text-brand-fg hover:opacity-90">
              <Plus size={15} /> Add approver
            </button>
          </div>
          <div className="divide-y divide-[var(--border)]">
            {isLoading && <div className="px-5 py-8 text-center text-sm text-[var(--muted)]">Loading…</div>}
            {data?.length === 0 && !draft && <div className="px-5 py-8 text-center text-sm text-[var(--muted)]">No approvers yet.</div>}
            {data?.map((a) => <Row key={a.id} approver={a} onSave={(i) => save.mutate(i)} onDelete={() => del.mutate(a.id)} saving={save.isPending} />)}
            {draft && <Editor initial={draft} onCancel={() => setDraft(null)} onSave={(i) => save.mutate(i)} saving={save.isPending} />}
          </div>
        </div>
      )}
    </div>
  );
}

function Row({ approver, onSave, onDelete, saving }: { approver: Approver; onSave: (i: ApproverInput) => void; onDelete: () => void; saving: boolean }) {
  const [editing, setEditing] = useState(false);
  if (editing) return <Editor initial={approver} onCancel={() => setEditing(false)} onSave={(i) => { onSave(i); setEditing(false); }} saving={saving} />;
  return (
    <div className="flex items-center gap-3 px-5 py-3">
      <div className="min-w-0 flex-1">
        <div className="font-medium">{approver.name}</div>
        <div className="truncate text-xs text-[var(--muted)]">
          {[approver.email, approver.phone].filter(Boolean).join(' · ') || '—'}{approver.scope ? ` — approves: ${approver.scope}` : ''}
        </div>
      </div>
      <button onClick={() => setEditing(true)} aria-label="Edit" className="rounded-md p-1.5 text-[var(--muted)] hover:bg-[var(--bg)] hover:text-brand"><Pencil size={15} /></button>
      <button onClick={() => { if (window.confirm(`Remove ${approver.name}?`)) onDelete(); }} aria-label="Delete" className="rounded-md p-1.5 text-[var(--muted)] hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-950/50"><Trash2 size={15} /></button>
    </div>
  );
}

function Editor({ initial, onCancel, onSave, saving }: { initial: Approver | ApproverInput; onCancel: () => void; onSave: (i: ApproverInput) => void; saving: boolean }) {
  const [f, setF] = useState<ApproverInput>({
    id: 'id' in initial ? initial.id : undefined,
    name: initial.name, email: initial.email ?? '', phone: initial.phone ?? '', scope: initial.scope ?? '',
    sortOrder: initial.sortOrder,
  });
  return (
    <div className="bg-[var(--bg)]/40 px-5 py-4">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <Field label="Name" value={f.name} onChange={(v) => setF({ ...f, name: v })} placeholder="Jane Doe" />
        <Field label="Email" value={f.email ?? ''} onChange={(v) => setF({ ...f, email: v })} placeholder="jane@company.com" type="email" />
        <Field label="Phone" value={f.phone ?? ''} onChange={(v) => setF({ ...f, phone: v })} placeholder="+1 555 0100" />
        <Field label="Approves (scope)" value={f.scope ?? ''} onChange={(v) => setF({ ...f, scope: v })} placeholder="New users, hardware under $500" />
      </div>
      <div className="mt-3 flex items-center justify-end gap-2">
        <button onClick={onCancel} className="inline-flex items-center gap-1.5 rounded-lg border border-[var(--border)] px-3 py-1.5 text-sm text-[var(--muted)] hover:bg-[var(--bg)]"><X size={14} /> Cancel</button>
        <button onClick={() => f.name.trim() && onSave(f)} disabled={!f.name.trim() || saving} className="inline-flex items-center gap-1.5 rounded-lg bg-brand px-3 py-1.5 text-sm font-medium text-brand-fg hover:opacity-90 disabled:opacity-40"><Check size={14} /> Save</button>
      </div>
    </div>
  );
}
