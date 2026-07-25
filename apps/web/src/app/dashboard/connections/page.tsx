'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plug, Plus, ShieldCheck } from 'lucide-react';
import { api } from '@/lib/api';

const PROVIDERS = [
  { value: 2, label: 'Datto Autotask', creds: ['ApiIntegrationCode', 'UserName', 'Secret'] },
  { value: 1, label: 'ConnectWise Manage', creds: ['CompanyId', 'PublicKey', 'PrivateKey', 'ClientId'] },
];

export default function ConnectionsPage() {
  const qc = useQueryClient();
  const { data, isError } = useQuery({ queryKey: ['connections'], queryFn: api.connections });
  const [open, setOpen] = useState(false);
  const [provider, setProvider] = useState(2);
  const [form, setForm] = useState<Record<string, string>>({ name: '', apiEndpoint: '' });

  const providerDef = PROVIDERS.find((p) => p.value === provider)!;

  const create = useMutation({
    mutationFn: () =>
      api.createConnection({
        name: form.name,
        provider,
        apiEndpoint: form.apiEndpoint,
        credentials: Object.fromEntries(providerDef.creds.map((c) => [c, form[c] ?? ''])),
      }),
    onSuccess: () => {
      setOpen(false);
      setForm({ name: '', apiEndpoint: '' });
      qc.invalidateQueries({ queryKey: ['connections'] });
    },
  });

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">PSA Connections</h1>
          <p className="text-sm text-[var(--muted)]">Connect Autotask and ConnectWise tenants.</p>
        </div>
        <button
          onClick={() => setOpen((v) => !v)}
          className="inline-flex items-center gap-2 rounded-lg bg-brand px-3.5 py-2 text-sm font-medium text-brand-fg hover:opacity-90"
        >
          <Plus size={16} /> Add connection
        </button>
      </div>

      {open && (
        <form
          onSubmit={(e) => {
            e.preventDefault();
            create.mutate();
          }}
          className="space-y-3 rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5"
        >
          <div className="flex items-center gap-2 rounded-lg bg-[var(--bg)] px-3 py-2 text-xs text-[var(--muted)]">
            <ShieldCheck size={14} /> Credentials are stored in your secret vault — never in the database or shown again.
          </div>
          <div className="grid gap-3 sm:grid-cols-2">
            <Input label="Name" value={form.name} onChange={(v) => setForm({ ...form, name: v })} />
            <label className="block">
              <span className="mb-1.5 block text-sm font-medium">Provider</span>
              <select
                value={provider}
                onChange={(e) => setProvider(Number(e.target.value))}
                className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm"
              >
                {PROVIDERS.map((p) => (
                  <option key={p.value} value={p.value}>{p.label}</option>
                ))}
              </select>
            </label>
            <Input label="API endpoint" value={form.apiEndpoint} onChange={(v) => setForm({ ...form, apiEndpoint: v })} />
          </div>
          <div className="grid gap-3 sm:grid-cols-2">
            {providerDef.creds.map((c) => (
              <Input key={c} label={c} type={/secret|key/i.test(c) ? 'password' : 'text'}
                value={form[c] ?? ''} onChange={(v) => setForm({ ...form, [c]: v })} />
            ))}
          </div>
          <div className="flex justify-end gap-2">
            <button type="button" onClick={() => setOpen(false)} className="rounded-lg border border-[var(--border)] px-3.5 py-2 text-sm">Cancel</button>
            <button type="submit" disabled={create.isPending} className="rounded-lg bg-brand px-4 py-2 text-sm font-medium text-brand-fg disabled:opacity-50">
              {create.isPending ? 'Saving…' : 'Save connection'}
            </button>
          </div>
          {create.isError && <p className="text-sm text-red-500">Could not save (preview runs without a backend).</p>}
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
              </tr>
            </thead>
            <tbody>
              {data.map((c) => (
                <tr key={c.id} className="border-b border-[var(--border)] last:border-0">
                  <td className="px-4 py-3 font-medium">{c.name}</td>
                  <td className="px-4 py-3 text-[var(--muted)]">{c.apiEndpoint}</td>
                  <td className="px-4 py-3">{String(c.status)}</td>
                  <td className="px-4 py-3">{c.isEnabled ? 'Yes' : 'No'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function Input({ label, value, onChange, type = 'text' }: { label: string; value: string; onChange: (v: string) => void; type?: string }) {
  return (
    <label className="block">
      <span className="mb-1.5 block text-sm font-medium">{label}</span>
      <input
        type={type}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm outline-none focus:border-brand"
      />
    </label>
  );
}
