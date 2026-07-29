'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowRight, SlidersHorizontal, Plus } from 'lucide-react';
import { api } from '@/lib/api';
import type { ConnectionFields } from '@/lib/types';

const SCOPE_CONNECTION = 2; // MappingScope.ConnectionOverride
const DIRECTION_BIDIRECTIONAL = 3; // MappingDirection.Bidirectional

// Status & priority have fixed portal-neutral values. Queue & category are open, so their portal
// values are defined per connection by adding mappings.
const FIELDS = [
  { key: 'status', label: 'Status', optionKey: 'statuses', fixed: ['NEW', 'IN_PROGRESS', 'WAITING_CUSTOMER', 'RESOLVED', 'CLOSED'] },
  { key: 'priority', label: 'Priority', optionKey: 'priorities', fixed: ['LOW', 'NORMAL', 'HIGH', 'CRITICAL'] },
  { key: 'queue', label: 'Queue / Board', optionKey: 'queuesOrBoards', fixed: null },
  { key: 'category', label: 'Category', optionKey: 'categories', fixed: null },
] as const;

type FieldKey = (typeof FIELDS)[number]['key'];

export default function MappingsPage() {
  const qc = useQueryClient();
  const { data: connections } = useQuery({ queryKey: ['connections'], queryFn: api.connections });
  const [connId, setConnId] = useState<string | null>(null);
  const [fieldKey, setFieldKey] = useState<FieldKey>('status');

  const conn = connections?.find((c) => c.id === connId);
  const provider = conn ? Number(conn.provider) : null;
  const fieldDef = FIELDS.find((f) => f.key === fieldKey)!;

  const { data: fields, isLoading: fieldsLoading } = useQuery({
    queryKey: ['fields', connId], queryFn: () => api.connectionFields(connId!), enabled: !!connId,
  });
  const { data: mappings } = useQuery({
    queryKey: ['mappings', provider], queryFn: () => api.listMappings(provider!), enabled: provider != null,
  });

  const options = fields ? (fields[fieldDef.optionKey as keyof ConnectionFields] ?? []) : [];
  const mine = (pv: string) =>
    mappings?.find((m) => m.psaConnectionId === connId && m.portalField === fieldKey && m.portalValue === pv);

  // Portal values to show: the fixed enum, or (for open fields) whatever mappings exist.
  const existingPortalValues = Array.from(
    new Set((mappings ?? [])
      .filter((m) => m.psaConnectionId === connId && m.portalField === fieldKey && m.portalValue)
      .map((m) => m.portalValue as string)),
  );
  const portalValues = fieldDef.fixed ?? existingPortalValues;

  const [overrides, setOverrides] = useState<Record<string, string>>({});
  useEffect(() => setOverrides({}), [connId, fieldKey]);
  const valueFor = (pv: string) => overrides[pv] ?? mine(pv)?.externalValue ?? '';

  function upsert(portalValue: string, externalValue: string, id?: string) {
    return api.upsertMapping({
      id, provider: provider!, scope: SCOPE_CONNECTION, psaConnectionId: connId!,
      portalField: fieldKey, portalValue, externalField: fieldKey, externalValue,
      direction: DIRECTION_BIDIRECTIONAL, isRequired: false, fallbackValue: null,
    }, `map ${fieldKey}`);
  }

  const save = useMutation({
    mutationFn: async () => {
      for (const pv of portalValues) {
        const ext = overrides[pv];
        if (ext === undefined || ext === '') continue;
        await upsert(pv, ext, mine(pv)?.id);
      }
    },
    onSuccess: () => { setOverrides({}); qc.invalidateQueries({ queryKey: ['mappings', provider] }); },
  });

  // Free-form add (queue / category)
  const [addPortal, setAddPortal] = useState('');
  const [addExternal, setAddExternal] = useState('');
  const add = useMutation({
    mutationFn: () => upsert(addPortal.trim(), addExternal),
    onSuccess: () => { setAddPortal(''); setAddExternal(''); qc.invalidateQueries({ queryKey: ['mappings', provider] }); },
  });

  const dirty = Object.values(overrides).some((v) => v !== '');

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <div>
        <h1 className="text-xl font-semibold">Field Mapping</h1>
        <p className="text-sm text-[var(--muted)]">
          Map the portal&apos;s neutral values to each PSA&apos;s real values — how your portal talks to the PSA.
        </p>
      </div>

      <div className="flex flex-wrap items-end gap-4">
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium">Connection</span>
          <select
            value={connId ?? ''}
            onChange={(e) => setConnId(e.target.value || null)}
            className="min-w-64 rounded-lg border border-[var(--border)] bg-[var(--surface)] px-3 py-2 text-sm"
          >
            <option value="">Select a connection…</option>
            {connections?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
        </label>

        <div className="inline-flex flex-wrap rounded-lg border border-[var(--border)] p-0.5">
          {FIELDS.map((f) => (
            <button
              key={f.key}
              onClick={() => setFieldKey(f.key)}
              className={`rounded-md px-3 py-1.5 text-sm font-medium ${
                fieldKey === f.key ? 'bg-brand text-brand-fg' : 'text-[var(--muted)] hover:text-[var(--fg)]'
              }`}
            >
              {f.label}
            </button>
          ))}
        </div>
      </div>

      {!connId && (
        <div className="flex flex-col items-center rounded-xl border border-dashed border-[var(--border)] px-6 py-12 text-center">
          <SlidersHorizontal className="mb-3 text-[var(--faint)]" size={26} />
          <p className="text-sm text-[var(--muted)]">Pick a connection to map its fields.</p>
        </div>
      )}

      {connId && fieldsLoading && <p className="text-sm text-[var(--muted)]">Discovering {fieldDef.label} values from the PSA…</p>}

      {connId && !fieldsLoading && (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)]">
          <div className="grid grid-cols-[1fr_auto_1.4fr] gap-3 border-b border-[var(--border)] px-5 py-3 text-xs font-semibold uppercase tracking-wide text-[var(--faint)]">
            <span>Portal {fieldDef.label}</span>
            <span />
            <span>PSA value</span>
          </div>

          <div className="divide-y divide-[var(--border)]">
            {portalValues.length === 0 && !fieldDef.fixed && (
              <p className="px-5 py-4 text-sm text-[var(--muted)]">No {fieldDef.label.toLowerCase()} mappings yet — add one below.</p>
            )}
            {portalValues.map((pv) => (
              <div key={pv} className="grid grid-cols-[1fr_auto_1.4fr] items-center gap-3 px-5 py-3">
                <span className="font-mono text-sm">{pv.replace(/_/g, ' ')}</span>
                <ArrowRight size={15} className="text-[var(--faint)]" />
                <select
                  value={valueFor(pv)}
                  onChange={(e) => setOverrides((m) => ({ ...m, [pv]: e.target.value }))}
                  className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm"
                >
                  <option value="">— not mapped —</option>
                  {options.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                </select>
              </div>
            ))}
          </div>

          {/* Free-form add for open fields (queue / category) */}
          {!fieldDef.fixed && (
            <form
              onSubmit={(e) => { e.preventDefault(); if (addPortal.trim() && addExternal) add.mutate(); }}
              className="grid grid-cols-[1fr_auto_1.4fr_auto] items-end gap-3 border-t border-[var(--border)] bg-[var(--bg)] px-5 py-3"
            >
              <label className="block">
                <span className="mb-1 block text-xs text-[var(--muted)]">New portal value</span>
                <input
                  value={addPortal}
                  onChange={(e) => setAddPortal(e.target.value)}
                  placeholder="e.g. NETWORK"
                  className="w-full rounded-lg border border-[var(--border)] bg-[var(--surface)] px-3 py-2 text-sm"
                />
              </label>
              <ArrowRight size={15} className="mb-2.5 text-[var(--faint)]" />
              <label className="block">
                <span className="mb-1 block text-xs text-[var(--muted)]">PSA value</span>
                <select
                  value={addExternal}
                  onChange={(e) => setAddExternal(e.target.value)}
                  className="w-full rounded-lg border border-[var(--border)] bg-[var(--surface)] px-3 py-2 text-sm"
                >
                  <option value="">Select…</option>
                  {options.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                </select>
              </label>
              <button
                type="submit"
                disabled={!addPortal.trim() || !addExternal || add.isPending}
                className="inline-flex items-center gap-1.5 rounded-lg border border-[var(--border)] bg-[var(--surface)] px-3 py-2 text-sm font-medium hover:bg-[var(--bg)] disabled:opacity-50"
              >
                <Plus size={14} /> Add
              </button>
            </form>
          )}

          <div className="flex items-center justify-between border-t border-[var(--border)] px-5 py-3">
            <span className="text-xs text-[var(--muted)]">
              {options.length} PSA {fieldDef.label.toLowerCase()} value{options.length === 1 ? '' : 's'} discovered · saved as a new version
            </span>
            <button
              onClick={() => save.mutate()}
              disabled={!dirty || save.isPending}
              className="rounded-lg bg-brand px-4 py-2 text-sm font-medium text-brand-fg hover:opacity-90 disabled:opacity-50"
            >
              {save.isPending ? 'Saving…' : 'Save mappings'}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
