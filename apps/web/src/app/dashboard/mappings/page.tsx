'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowRight, SlidersHorizontal } from 'lucide-react';
import { api } from '@/lib/api';

// Portal-neutral values the platform normalizes to. These are mapped to each PSA's real values.
const PORTAL_VALUES: Record<string, string[]> = {
  status: ['NEW', 'IN_PROGRESS', 'WAITING_CUSTOMER', 'RESOLVED', 'CLOSED'],
  priority: ['LOW', 'NORMAL', 'HIGH', 'CRITICAL'],
};
const FIELDS = [
  { key: 'status', label: 'Status' },
  { key: 'priority', label: 'Priority' },
] as const;

const SCOPE_CONNECTION = 2; // MappingScope.ConnectionOverride
const DIRECTION_BIDIRECTIONAL = 3; // MappingDirection.Bidirectional

export default function MappingsPage() {
  const qc = useQueryClient();
  const { data: connections } = useQuery({ queryKey: ['connections'], queryFn: api.connections });
  const [connId, setConnId] = useState<string | null>(null);
  const [field, setField] = useState<'status' | 'priority'>('status');

  const conn = connections?.find((c) => c.id === connId);
  const provider = conn ? Number(conn.provider) : null;

  const { data: fields, isLoading: fieldsLoading } = useQuery({
    queryKey: ['fields', connId], queryFn: () => api.connectionFields(connId!), enabled: !!connId,
  });
  const { data: mappings } = useQuery({
    queryKey: ['mappings', provider], queryFn: () => api.listMappings(provider!), enabled: provider != null,
  });

  const options = field === 'status' ? fields?.statuses ?? [] : fields?.priorities ?? [];
  const portalValues = PORTAL_VALUES[field];

  const existing = (pv: string) =>
    mappings?.find((m) => m.psaConnectionId === connId && m.portalField === field && m.portalValue === pv);

  const [overrides, setOverrides] = useState<Record<string, string>>({});
  useEffect(() => setOverrides({}), [connId, field]);
  const valueFor = (pv: string) => overrides[pv] ?? existing(pv)?.externalValue ?? '';

  const save = useMutation({
    mutationFn: async () => {
      for (const pv of portalValues) {
        const ext = overrides[pv];
        if (ext === undefined || ext === '') continue; // unchanged or cleared
        await api.upsertMapping({
          id: existing(pv)?.id,
          provider: provider!,
          scope: SCOPE_CONNECTION,
          psaConnectionId: connId!,
          portalField: field,
          portalValue: pv,
          externalField: field,
          externalValue: ext,
          direction: DIRECTION_BIDIRECTIONAL,
          isRequired: false,
          fallbackValue: null,
        }, `map ${field}`);
      }
    },
    onSuccess: () => {
      setOverrides({});
      qc.invalidateQueries({ queryKey: ['mappings', provider] });
    },
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

      {/* Connection + field selectors */}
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

        <div className="inline-flex rounded-lg border border-[var(--border)] p-0.5">
          {FIELDS.map((f) => (
            <button
              key={f.key}
              onClick={() => setField(f.key)}
              className={`rounded-md px-3 py-1.5 text-sm font-medium ${
                field === f.key ? 'bg-brand text-brand-fg' : 'text-[var(--muted)] hover:text-[var(--fg)]'
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

      {connId && fieldsLoading && (
        <p className="text-sm text-[var(--muted)]">Discovering {field} values from the PSA…</p>
      )}

      {connId && !fieldsLoading && (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)]">
          <div className="grid grid-cols-[1fr_auto_1.4fr] gap-3 border-b border-[var(--border)] px-5 py-3 text-xs font-semibold uppercase tracking-wide text-[var(--faint)]">
            <span>Portal {field}</span>
            <span />
            <span>PSA value</span>
          </div>
          <div className="divide-y divide-[var(--border)]">
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
          <div className="flex items-center justify-between border-t border-[var(--border)] px-5 py-3">
            <span className="text-xs text-[var(--muted)]">
              {options.length} PSA {field} value{options.length === 1 ? '' : 's'} discovered · saved as a new version
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
