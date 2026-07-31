'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Server, Building2, Plus, Pencil, Trash2, Check, X, CheckCircle2, Info } from 'lucide-react';
import { api, type Device, type DeviceInput } from '@/lib/api';
import { CpHeader, AccessError, Field } from '../_ui';

export default function AccountsPage() {
  const qc = useQueryClient();
  const { data: account, error } = useQuery({ queryKey: ['cp-account'], queryFn: api.cpAccount, retry: false });
  const { data: devices, isLoading } = useQuery({ queryKey: ['cp-devices'], queryFn: api.cpDevices, retry: false, enabled: !error });
  const [draft, setDraft] = useState<DeviceInput | null>(null);

  const save = useMutation({
    mutationFn: (input: DeviceInput) => api.cpSaveDevice(input),
    onSuccess: () => { setDraft(null); qc.invalidateQueries({ queryKey: ['cp-devices'] }); },
  });
  const del = useMutation({
    mutationFn: (id: string) => api.cpDeleteDevice(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['cp-devices'] }),
  });

  return (
    <div className="mx-auto max-w-4xl space-y-5">
      <CpHeader icon={Server} title="Accounts & Devices" subtitle="Your account details (synced from the PSA) and the devices you want your technicians to know about." />

      {error ? <AccessError label="Accounts & Devices" /> : (
        <>
          {/* Account card — read-only, from PSA sync */}
          <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5">
            <div className="flex items-center gap-2.5">
              <span className="inline-flex h-10 w-10 items-center justify-center rounded-lg bg-brand/10 text-brand"><Building2 size={18} /></span>
              <div>
                <div className="text-sm font-semibold">{account?.name ?? '—'}</div>
                <div className="text-xs text-[var(--muted)]">
                  {account?.connectionName ? `${account.connectionName} · ` : ''}PSA id {account?.externalCompanyId ?? '—'}
                  {account && (account.isActive
                    ? <span className="ml-2 inline-flex items-center gap-1 text-green-600 dark:text-green-400"><CheckCircle2 size={11} /> Active</span>
                    : <span className="ml-2 text-[var(--faint)]">Inactive</span>)}
                </div>
              </div>
            </div>
            <p className="mt-3 flex items-center gap-1.5 text-xs text-[var(--faint)]"><Info size={12} /> Account details come from your PSA and are read-only here.</p>
          </div>

          {/* Devices */}
          <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)]">
            <div className="flex items-center justify-between border-b border-[var(--border)] px-5 py-3.5">
              <h2 className="text-sm font-semibold">Devices</h2>
              <button onClick={() => setDraft({ name: '', type: '', identifier: '', notes: '' })}
                className="inline-flex items-center gap-1.5 rounded-lg bg-brand px-3 py-1.5 text-sm font-medium text-brand-fg hover:opacity-90">
                <Plus size={15} /> Add device
              </button>
            </div>
            <div className="divide-y divide-[var(--border)]">
              {isLoading && <div className="px-5 py-8 text-center text-sm text-[var(--muted)]">Loading…</div>}
              {devices?.length === 0 && !draft && <div className="px-5 py-8 text-center text-sm text-[var(--muted)]">No devices recorded yet.</div>}
              {devices?.map((d) => <Row key={d.id} device={d} onSave={(i) => save.mutate(i)} onDelete={() => del.mutate(d.id)} saving={save.isPending} />)}
              {draft && <Editor initial={draft} onCancel={() => setDraft(null)} onSave={(i) => save.mutate(i)} saving={save.isPending} />}
            </div>
          </div>
        </>
      )}
    </div>
  );
}

function Row({ device, onSave, onDelete, saving }: { device: Device; onSave: (i: DeviceInput) => void; onDelete: () => void; saving: boolean }) {
  const [editing, setEditing] = useState(false);
  if (editing) return <Editor initial={device} onCancel={() => setEditing(false)} onSave={(i) => { onSave(i); setEditing(false); }} saving={saving} />;
  return (
    <div className="flex items-center gap-3 px-5 py-3">
      <Server size={16} className="shrink-0 text-[var(--muted)]" />
      <div className="min-w-0 flex-1">
        <div className="font-medium">{device.name} {device.type && <span className="ml-1 rounded bg-[var(--bg)] px-1.5 py-0.5 text-[11px] font-medium text-[var(--muted)]">{device.type}</span>}</div>
        <div className="truncate text-xs text-[var(--muted)]">{[device.identifier, device.notes].filter(Boolean).join(' · ') || '—'}</div>
      </div>
      <button onClick={() => setEditing(true)} aria-label="Edit" className="rounded-md p-1.5 text-[var(--muted)] hover:bg-[var(--bg)] hover:text-brand"><Pencil size={15} /></button>
      <button onClick={() => { if (window.confirm(`Remove ${device.name}?`)) onDelete(); }} aria-label="Delete" className="rounded-md p-1.5 text-[var(--muted)] hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-950/50"><Trash2 size={15} /></button>
    </div>
  );
}

function Editor({ initial, onCancel, onSave, saving }: { initial: Device | DeviceInput; onCancel: () => void; onSave: (i: DeviceInput) => void; saving: boolean }) {
  const [f, setF] = useState<DeviceInput>({
    id: 'id' in initial ? initial.id : undefined,
    name: initial.name, type: initial.type ?? '', identifier: initial.identifier ?? '', notes: initial.notes ?? '',
  });
  return (
    <div className="bg-[var(--bg)]/40 px-5 py-4">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <Field label="Name" value={f.name} onChange={(v) => setF({ ...f, name: v })} placeholder="Reception PC" />
        <Field label="Type" value={f.type ?? ''} onChange={(v) => setF({ ...f, type: v })} placeholder="Workstation / Server / Firewall" />
        <Field label="Identifier" value={f.identifier ?? ''} onChange={(v) => setF({ ...f, identifier: v })} placeholder="Asset tag / serial / hostname" />
        <Field label="Notes" value={f.notes ?? ''} onChange={(v) => setF({ ...f, notes: v })} placeholder="Ground floor, front desk" />
      </div>
      <div className="mt-3 flex items-center justify-end gap-2">
        <button onClick={onCancel} className="inline-flex items-center gap-1.5 rounded-lg border border-[var(--border)] px-3 py-1.5 text-sm text-[var(--muted)] hover:bg-[var(--bg)]"><X size={14} /> Cancel</button>
        <button onClick={() => f.name.trim() && onSave(f)} disabled={!f.name.trim() || saving} className="inline-flex items-center gap-1.5 rounded-lg bg-brand px-3 py-1.5 text-sm font-medium text-brand-fg hover:opacity-90 disabled:opacity-40"><Check size={14} /> Save</button>
      </div>
    </div>
  );
}
