'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plug, Plus, ShieldCheck, ChevronDown } from 'lucide-react';
import { api } from '@/lib/api';
import type { ConnectionSummary, ConnectionFields } from '@/lib/types';
import { SyncSettings } from './SyncSettings';

const PROVIDERS = [
  { value: 2, label: 'Datto Autotask', creds: ['ApiIntegrationCode', 'UserName', 'Secret'] },
  { value: 1, label: 'ConnectWise Manage', creds: ['CompanyId', 'PublicKey', 'PrivateKey', 'ClientId'] },
];

// ConnectionStatus enum: 0 Disabled, 1 Pending, 2 Healthy, 3 Degraded, 4 Failed
const STATUS_LABEL: Record<number, string> = { 0: 'Disabled', 1: 'Pending', 2: 'Healthy', 3: 'Degraded', 4: 'Failed' };

export default function ConnectionsPage() {
  const qc = useQueryClient();
  const { data, isError } = useQuery({ queryKey: ['connections'], queryFn: api.connections });

  const [open, setOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [provider, setProvider] = useState(2);
  const [form, setForm] = useState<Record<string, string>>({ name: '', apiEndpoint: '', tenantIdentifier: '' });
  const providerDef = PROVIDERS.find((p) => p.value === provider)!;

  function openAdd() {
    setEditingId(null);
    setProvider(2);
    setForm({ name: '', apiEndpoint: '', tenantIdentifier: '' });
    setOpen(true);
  }
  function openEdit(c: ConnectionSummary) {
    setEditingId(c.id);
    setProvider(Number(c.provider));
    setForm({ name: c.name, apiEndpoint: c.apiEndpoint, tenantIdentifier: c.tenantIdentifier ?? '' });
    setOpen(true);
  }

  const save = useMutation({
    mutationFn: () => {
      const entered = Object.fromEntries(providerDef.creds.map((c) => [c, form[c] ?? '']).filter(([, v]) => v !== ''));
      if (editingId) {
        return api.updateConnection(editingId, {
          name: form.name,
          apiEndpoint: form.apiEndpoint,
          tenantIdentifier: form.tenantIdentifier || undefined,
          isEnabled: true,
          credentials: Object.keys(entered).length ? entered : undefined,
        });
      }
      return api.createConnection({
        name: form.name,
        provider,
        apiEndpoint: form.apiEndpoint,
        tenantIdentifier: form.tenantIdentifier || undefined,
        credentials: Object.fromEntries(providerDef.creds.map((c) => [c, form[c] ?? ''])),
      });
    },
    onSuccess: () => {
      setOpen(false);
      qc.invalidateQueries({ queryKey: ['connections'] });
    },
  });

  const [results, setResults] = useState<Record<string, { ok: boolean; msg: string }>>({});
  const test = useMutation({
    mutationFn: (id: string) => api.testConnection(id),
    onSuccess: (r, id) => {
      setResults((m) => ({ ...m, [id]: { ok: r.success, msg: r.success ? `Healthy · ${Math.round(r.latencyMs)}ms` : r.message ?? 'Failed' } }));
      qc.invalidateQueries({ queryKey: ['connections'] });
    },
    onError: (_e, id) => setResults((m) => ({ ...m, [id]: { ok: false, msg: 'Request failed' } })),
  });

  const sync = useMutation({
    mutationFn: (v: { id: string; full: boolean }) => api.syncConnection(v.id, v.full),
    onSuccess: (r, v) => {
      const id = v.id;
      setResults((m) => ({ ...m, [id]: { ok: true, msg: `${v.full ? 'Re-synced all' : 'Synced'} · ${r.created} new, ${r.updated} updated (${r.fetched} fetched)` } }));
      // Refresh every page that reads synced data.
      ['connections', 'tickets', 'team', 'trend', 'health', 'notifications', 'audit', 'jobs'].forEach((k) =>
        qc.invalidateQueries({ queryKey: [k] }));
    },
    onError: (e, v) => setResults((m) => ({ ...m, [v.id]: { ok: false, msg: e instanceof Error ? `Sync failed: ${e.message}` : 'Sync failed' } })),
  });

  const refreshFields = useMutation({
    mutationFn: (id: string) => api.refreshConnectionFields(id),
    onSuccess: (f, id) => {
      setFieldsById((m) => ({ ...m, [id]: f }));
      setExpandedId(id);
      setResults((m) => ({ ...m, [id]: { ok: true, msg: `Fields refreshed · ${f.statuses.length} statuses, ${f.workTypes.length} work types` } }));
      qc.invalidateQueries({ queryKey: ['connections'] });
    },
    onError: (_e, id) => setResults((m) => ({ ...m, [id]: { ok: false, msg: 'Field refresh failed' } })),
  });

  const [settingsId, setSettingsId] = useState<string | null>(null);

  // Live field discovery (boards/queues, statuses, priorities, categories)
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [fieldsById, setFieldsById] = useState<Record<string, ConnectionFields | 'loading' | 'error'>>({});
  async function toggleFields(id: string) {
    if (expandedId === id) { setExpandedId(null); return; }
    setExpandedId(id);
    if (!fieldsById[id] || fieldsById[id] === 'error') {
      setFieldsById((m) => ({ ...m, [id]: 'loading' }));
      try {
        const discovered = await api.connectionFields(id);
        setFieldsById((m) => ({ ...m, [id]: discovered }));
      } catch {
        setFieldsById((m) => ({ ...m, [id]: 'error' }));
      }
    }
  }

  const isEdit = editingId !== null;

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">PSA Connections</h1>
          <p className="text-sm text-[var(--muted)]">Connect Autotask and ConnectWise tenants.</p>
        </div>
        <button onClick={openAdd} className="inline-flex items-center gap-2 rounded-lg bg-brand px-3.5 py-2 text-sm font-medium text-brand-fg hover:opacity-90">
          <Plus size={16} /> Add connection
        </button>
      </div>

      {open && (
        <form
          onSubmit={(e) => { e.preventDefault(); save.mutate(); }}
          className="space-y-3 rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5"
        >
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-semibold">{isEdit ? 'Edit connection' : 'New connection'}</h2>
          </div>
          <div className="flex items-center gap-2 rounded-lg bg-[var(--bg)] px-3 py-2 text-xs text-[var(--muted)]">
            <ShieldCheck size={14} /> Credentials are stored in your secret vault — never in the database or shown again.
            {isEdit && ' Leave the credential fields blank to keep the existing keys.'}
          </div>
          <div className="grid gap-3 sm:grid-cols-2">
            <Input label="Name" value={form.name} onChange={(v) => setForm({ ...form, name: v })} />
            <label className="block">
              <span className="mb-1.5 block text-sm font-medium">Provider</span>
              <select
                value={provider}
                disabled={isEdit}
                onChange={(e) => setProvider(Number(e.target.value))}
                className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm disabled:opacity-60"
              >
                {PROVIDERS.map((p) => <option key={p.value} value={p.value}>{p.label}</option>)}
              </select>
            </label>
            <Input label="API endpoint" value={form.apiEndpoint} onChange={(v) => setForm({ ...form, apiEndpoint: v })}
              placeholder={provider === 2 ? 'https://webservices31.autotask.net/ATServicesRest/' : 'https://api-na.myconnectwise.net/v4_6_release/apis/3.0/'}
              hint={provider === 2
                ? 'Your Autotask zone URL. The version segment is optional — /ATServicesRest/ and /ATServicesRest/v1.0/ both work.'
                : 'Your ConnectWise API base, ending in /apis/3.0/.'} />
            <Input label="Tenant identifier (optional)" value={form.tenantIdentifier} onChange={(v) => setForm({ ...form, tenantIdentifier: v })}
              hint="A label for your own reference — not required, and not sent to the PSA." />
          </div>
          <div className="grid gap-3 sm:grid-cols-2">
            {providerDef.creds.map((c) => (
              <Input key={c} label={c} type={/secret|key/i.test(c) ? 'password' : 'text'}
                placeholder={isEdit ? 'unchanged' : ''}
                value={form[c] ?? ''} onChange={(v) => setForm({ ...form, [c]: v })} />
            ))}
          </div>
          <div className="flex justify-end gap-2">
            <button type="button" onClick={() => setOpen(false)} className="rounded-lg border border-[var(--border)] px-3.5 py-2 text-sm">Cancel</button>
            <button type="submit" disabled={save.isPending} className="rounded-lg bg-brand px-4 py-2 text-sm font-medium text-brand-fg disabled:opacity-50">
              {save.isPending ? 'Saving…' : isEdit ? 'Save changes' : 'Save connection'}
            </button>
          </div>
          {save.isError && <p className="text-sm text-red-500">Could not save (preview runs without a backend).</p>}
        </form>
      )}

      {(isError || (data && data.length === 0)) && !open && (
        <div className="flex flex-col items-center rounded-xl border border-dashed border-[var(--border)] px-6 py-12 text-center">
          <Plug className="mb-3 text-[var(--faint)]" size={26} />
          <p className="text-sm text-[var(--muted)]">No connections yet. Add one to start syncing tickets.</p>
        </div>
      )}

      {data && data.length > 0 && (
        <div className="overflow-x-auto rounded-xl border border-[var(--border)] bg-[var(--surface)]">
          <table className="w-full text-sm">
            <thead className="text-left text-xs uppercase tracking-wide text-[var(--muted)]">
              <tr className="border-b border-[var(--border)]">
                <th className="px-4 py-3 font-medium">Name</th>
                <th className="px-4 py-3 font-medium">Endpoint</th>
                <th className="px-4 py-3 font-medium">Status</th>
                <th className="px-4 py-3 font-medium">Enabled</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody>
              {data.map((c) => (
                <FragmentRow
                  key={c.id}
                  c={c}
                  result={results[c.id]}
                  expanded={expandedId === c.id}
                  fields={fieldsById[c.id]}
                  onTest={() => test.mutate(c.id)}
                  testing={test.isPending && test.variables === c.id}
                  onSync={() => sync.mutate({ id: c.id, full: false })}
                  onResyncAll={() => sync.mutate({ id: c.id, full: true })}
                  syncing={sync.isPending && sync.variables?.id === c.id}
                  settingsOpen={settingsId === c.id}
                  onToggleSettings={() => setSettingsId(settingsId === c.id ? null : c.id)}
                  onRefreshFields={() => refreshFields.mutate(c.id)}
                  refreshingFields={refreshFields.isPending && refreshFields.variables === c.id}
                  onEdit={() => openEdit(c)}
                  onToggleFields={() => toggleFields(c.id)}
                />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function FragmentRow({
  c, result, expanded, fields, onTest, testing, onSync, onResyncAll, syncing, settingsOpen, onToggleSettings, onRefreshFields, refreshingFields, onEdit, onToggleFields,
}: {
  c: ConnectionSummary;
  result?: { ok: boolean; msg: string };
  expanded: boolean;
  fields?: ConnectionFields | 'loading' | 'error';
  onTest: () => void;
  testing: boolean;
  onSync: () => void;
  onResyncAll: () => void;
  syncing: boolean;
  settingsOpen: boolean;
  onToggleSettings: () => void;
  onRefreshFields: () => void;
  refreshingFields: boolean;
  onEdit: () => void;
  onToggleFields: () => void;
}) {
  return (
    <>
      <tr className="border-b border-[var(--border)] last:border-0">
        <td className="px-4 py-3 font-medium">{c.name}</td>
        <td className="px-4 py-3 text-[var(--muted)]">{c.apiEndpoint}</td>
        <td className="px-4 py-3">{STATUS_LABEL[Number(c.status)] ?? String(c.status)}</td>
        <td className="px-4 py-3">{c.isEnabled ? 'Yes' : 'No'}</td>
        <td className="px-4 py-3 text-right">
          <div className="flex items-center justify-end gap-2">
            {result && (
              <span className={result.ok ? 'text-xs text-green-600 dark:text-green-400' : 'text-xs text-red-600 dark:text-red-400'}>
                {result.msg}
              </span>
            )}
            <ActionButton onClick={onToggleFields}>
              <ChevronDown size={13} className={expanded ? 'rotate-180 transition-transform' : 'transition-transform'} /> Boards
            </ActionButton>
            <ActionButton onClick={onRefreshFields} disabled={refreshingFields}>{refreshingFields ? 'Refreshing…' : 'Refresh fields'}</ActionButton>
            <ActionButton onClick={onEdit}>Edit</ActionButton>
            <ActionButton onClick={onTest} disabled={testing}>{testing ? 'Testing…' : 'Test'}</ActionButton>
            <ActionButton onClick={onSync} disabled={syncing}>{syncing ? 'Syncing…' : 'Sync now'}</ActionButton>
            <ActionButton onClick={onResyncAll} disabled={syncing}>Re-sync all</ActionButton>
            <ActionButton onClick={onToggleSettings}>
              <ChevronDown size={13} className={settingsOpen ? 'rotate-180 transition-transform' : 'transition-transform'} /> Sync settings
            </ActionButton>
          </div>
        </td>
      </tr>
      {settingsOpen && (
        <tr className="border-b border-[var(--border)] bg-[var(--bg)]">
          <td colSpan={5} className="px-4 py-4">
            <SyncSettings connectionId={c.id} provider={Number(c.provider)} />
          </td>
        </tr>
      )}
      {expanded && (
        <tr className="border-b border-[var(--border)] bg-[var(--bg)]">
          <td colSpan={5} className="px-4 py-4">
            {fields === 'loading' && <p className="text-sm text-[var(--muted)]">Discovering fields from the PSA…</p>}
            {fields === 'error' && <p className="text-sm text-red-500">Couldn&apos;t load fields (connection may be unreachable).</p>}
            {fields && fields !== 'loading' && fields !== 'error' && (
              <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
                <FieldList title="Service boards / queues" items={fields.queuesOrBoards} />
                <FieldList title="Statuses" items={fields.statuses} />
                <FieldList title="Priorities" items={fields.priorities} />
                <FieldList title="Categories" items={fields.categories} />
              </div>
            )}
          </td>
        </tr>
      )}
    </>
  );
}

function ActionButton({ children, onClick, disabled }: { children: React.ReactNode; onClick: () => void; disabled?: boolean }) {
  return (
    <button
      onClick={onClick}
      disabled={disabled}
      className="inline-flex items-center gap-1.5 rounded-lg border border-[var(--border)] px-2.5 py-1.5 text-xs font-medium hover:bg-[var(--surface)] disabled:opacity-50"
    >
      {children}
    </button>
  );
}

function FieldList({ title, items }: { title: string; items: { value: string; label: string }[] }) {
  return (
    <div>
      <div className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-[var(--faint)]">
        {title} <span className="text-[var(--muted)]">({items.length})</span>
      </div>
      {items.length === 0 ? (
        <p className="text-xs text-[var(--muted)]">—</p>
      ) : (
        <ul className="space-y-1">
          {items.slice(0, 12).map((o) => (
            <li key={o.value} className="text-sm">{o.label}</li>
          ))}
          {items.length > 12 && <li className="text-xs text-[var(--muted)]">+{items.length - 12} more</li>}
        </ul>
      )}
    </div>
  );
}

function Input({ label, value, onChange, type = 'text', placeholder, hint }: { label: string; value: string; onChange: (v: string) => void; type?: string; placeholder?: string; hint?: string }) {
  return (
    <label className="block">
      <span className="mb-1.5 block text-sm font-medium">{label}</span>
      <input
        type={type}
        value={value}
        placeholder={placeholder}
        onChange={(e) => onChange(e.target.value)}
        className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm outline-none focus:border-brand"
      />
      {hint && <span className="mt-1 block text-xs text-[var(--muted)]">{hint}</span>}
    </label>
  );
}
