'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { RefreshCw, Filter, Save, CheckCircle2, AlertTriangle, Settings2 } from 'lucide-react';
import { api, type ConnectionSettings } from '@/lib/api';

// Provider-neutral labels: ConnectWise calls a queue a "service board", Autotask calls it a queue.
// ProviderType: 1 = ConnectWise, 2 = Autotask.
const queueLabel = (provider: number) => (provider === 1 ? 'Service Board IDs' : 'Queue IDs');
const resourceLabel = (provider: number) => (provider === 1 ? 'Member IDs' : 'Resource IDs');

function Toggle({ label, hint, checked, onChange, disabled, notYet }: {
  label: string; hint: string; checked: boolean; onChange: (v: boolean) => void; disabled?: boolean; notYet?: boolean;
}) {
  const off = disabled || notYet;
  return (
    <label className={`flex gap-2.5 rounded-lg border border-[var(--border)] p-3 ${off ? 'opacity-60' : 'cursor-pointer hover:bg-[var(--bg)]'}`}>
      <input type="checkbox" checked={checked && !notYet} disabled={off}
        onChange={(e) => onChange(e.target.checked)} className="mt-0.5 h-4 w-4 shrink-0 accent-[var(--brand-line,#3b82f6)]" />
      <span>
        <span className="flex items-center gap-1.5 text-sm font-medium">
          {label}
          {notYet && <span className="rounded-full bg-[var(--bg)] px-1.5 py-0.5 text-[9px] font-semibold uppercase tracking-wide text-[var(--faint)]">Not yet active</span>}
        </span>
        <span className="block text-xs text-[var(--muted)]">{hint}</span>
      </span>
    </label>
  );
}

function IdField({ label, hint, value, onChange }: {
  label: string; hint: string; value: string; onChange: (v: string) => void;
}) {
  return (
    <label className="block">
      <span className="mb-1 block text-xs font-medium">{label}</span>
      <input value={value} onChange={(e) => onChange(e.target.value)} placeholder="empty = no restriction"
        className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm outline-none focus:border-brand" />
      <span className="mt-1 block text-xs text-[var(--muted)]">{hint}</span>
    </label>
  );
}

/** Per-connection sync behaviour + import filters. Ids are comma-separated; use "Boards" to look them up. */
export function SyncSettings({ connectionId, provider }: { connectionId: string; provider: number }) {
  const qc = useQueryClient();
  const { data, isLoading } = useQuery({
    queryKey: ['connection-settings', connectionId],
    queryFn: () => api.connectionSettings(connectionId),
  });
  // Defaults are picked from live discovery, so admins never transcribe numeric ids by hand.
  const { data: fields } = useQuery({
    queryKey: ['fields', connectionId],
    queryFn: () => api.connectionFields(connectionId),
    retry: false,
  });
  const [form, setForm] = useState<ConnectionSettings | null>(null);
  useEffect(() => { if (data) setForm(data); }, [data]);

  const save = useMutation({
    mutationFn: (body: ConnectionSettings) => api.saveConnectionSettings(connectionId, body),
    onSuccess: (saved) => {
      setForm(saved); // server normalizes (e.g. notes off when two-way is off)
      qc.invalidateQueries({ queryKey: ['connection-settings', connectionId] });
    },
  });

  if (isLoading || !form) return <p className="text-sm text-[var(--muted)]">Loading sync settings…</p>;
  const set = <K extends keyof ConnectionSettings>(k: K, v: ConnectionSettings[K]) => setForm({ ...form, [k]: v });

  return (
    <div className="space-y-4">
      <div>
        <h3 className="flex items-center gap-2 text-sm font-semibold"><RefreshCw size={15} className="text-brand" /> Synchronisation</h3>
        <p className="text-xs text-[var(--muted)]">What flows automatically between the portal and this PSA.</p>
        <div className="mt-2 grid gap-2 sm:grid-cols-2">
          <Toggle label="Two-way sync" checked={form.twoWaySync} onChange={(v) => set('twoWaySync', v)}
            hint="Pull provider-side changes back into the portal. Off = portal → PSA writes only." />
          <Toggle label="Auto-import new tickets" checked={form.autoImportNewTickets} onChange={(v) => set('autoImportNewTickets', v)}
            hint="Create brand-new provider tickets here on each sync. Off = only tickets already known are updated." />
          <Toggle label="Import notes" checked={form.importNotes} onChange={(v) => set('importNotes', v)}
            hint="Mirror the provider's public notes into the portal thread. Internal/private notes are never imported." />
          <Toggle label="Import system notes" checked={form.importSystemNotes} onChange={(v) => set('importSystemNotes', v)}
            hint="Also import notes with no human author (workflow/SLA automation). Depends on note import." />
          <Toggle label="Sync attachments" notYet checked={form.syncAttachments} onChange={(v) => set('syncAttachments', v)}
            hint="Will mirror attachments both ways. Portal-side upload works today; provider sync is not built yet." />
        </div>
      </div>

      <div>
        <h3 className="flex items-center gap-2 text-sm font-semibold"><Filter size={15} className="text-brand" /> Import filters</h3>
        <p className="text-xs text-[var(--muted)]">Which of this PSA&apos;s tickets are yours to import. Lists are comma-separated ids — use <strong>Boards</strong> to look them up.</p>
        <div className="mt-2 grid gap-2 sm:grid-cols-2">
          <Toggle label="Open / active tickets" checked={form.importOpenTickets} onChange={(v) => set('importOpenTickets', v)} hint="Import tickets that are not yet resolved or closed." />
          <Toggle label="Completed / closed tickets" checked={form.importClosedTickets} onChange={(v) => set('importClosedTickets', v)} hint="Import tickets the provider considers finished." />
        </div>
        <div className="mt-3 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <IdField label="Limit to Company IDs" hint="Only these customers." value={form.filterCompanyIds ?? ''} onChange={(v) => set('filterCompanyIds', v)} />
          <IdField label={`Limit to ${queueLabel(provider)}`} hint={provider === 1 ? 'CW service boards.' : 'Autotask queues.'} value={form.filterQueueIds ?? ''} onChange={(v) => set('filterQueueIds', v)} />
          <IdField label={`Limit to ${resourceLabel(provider)}`} hint="Only tickets assigned to these techs." value={form.filterResourceIds ?? ''} onChange={(v) => set('filterResourceIds', v)} />
          <label className="block">
            <span className="mb-1 block text-xs font-medium">Active in the last N days</span>
            <input type="number" min="0" value={form.filterActiveWithinDays ?? ''} placeholder="no limit"
              onChange={(e) => set('filterActiveWithinDays', e.target.value === '' ? null : Number(e.target.value))}
              className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm outline-none focus:border-brand" />
            <span className="mt-1 block text-xs text-[var(--muted)]">7 is a good start for a new connection.</span>
          </label>
        </div>
      </div>

      <div>
        <h3 className="flex items-center gap-2 text-sm font-semibold"><Settings2 size={15} className="text-brand" /> Ticket defaults</h3>
        <p className="text-xs text-[var(--muted)]">
          Sent when the portal creates a ticket here. Providers mandate different fields — Autotask
          requires a queue — and the portal&apos;s form stays simple, so the connection supplies the rest.
        </p>
        <div className="mt-2 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <label className="block">
            <span className="mb-1 block text-xs font-medium">Default {provider === 1 ? 'board' : 'queue'}</span>
            <select value={form.defaultQueueOrBoardId ?? ''} onChange={(e) => set('defaultQueueOrBoardId', e.target.value || null)}
              className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm outline-none focus:border-brand">
              <option value="">— none —</option>
              {(fields?.queuesOrBoards ?? []).map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
            </select>
            <span className="mt-1 block text-xs text-[var(--muted)]">Required by Autotask on create.</span>
          </label>
          <label className="block">
            <span className="mb-1 block text-xs font-medium">Default {provider === 1 ? 'type' : 'ticket type'}</span>
            <select value={form.defaultTicketType ?? ''} onChange={(e) => set('defaultTicketType', e.target.value || null)}
              className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm outline-none focus:border-brand">
              <option value="">— none —</option>
              {(fields?.categories ?? []).map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
            </select>
          </label>
          <label className="block">
            <span className="mb-1 block text-xs font-medium">Default {provider === 1 ? 'sub-type' : 'issue type'}</span>
            <input value={form.defaultIssueType ?? ''} onChange={(e) => set('defaultIssueType', e.target.value || null)}
              placeholder="optional id"
              className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm outline-none focus:border-brand" />
            <span className="mt-1 block text-xs text-[var(--muted)]">Only if your tenant requires it.</span>
          </label>
          <label className="block">
            <span className="mb-1 block text-xs font-medium">Default {provider === 1 ? 'item' : 'sub-issue type'}</span>
            <input value={form.defaultSubIssueType ?? ''} onChange={(e) => set('defaultSubIssueType', e.target.value || null)}
              placeholder="optional id"
              className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm outline-none focus:border-brand" />
          </label>
        </div>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-2 border-t border-[var(--border)] pt-3">
        <div className="text-xs">
          {save.isError && <span className="inline-flex items-center gap-1 text-red-600 dark:text-red-400"><AlertTriangle size={13} /> {(save.error as Error)?.message ?? 'Save failed'}</span>}
          {save.isSuccess && !save.isPending && <span className="inline-flex items-center gap-1 text-green-600 dark:text-green-400"><CheckCircle2 size={13} /> Saved — applies on the next sync.</span>}
          {!form.importOpenTickets && !form.importClosedTickets && <span className="text-amber-600 dark:text-amber-400">Select at least one of open or closed.</span>}
        </div>
        <button onClick={() => save.mutate(form)} disabled={save.isPending || (!form.importOpenTickets && !form.importClosedTickets)}
          className="inline-flex items-center gap-2 rounded-lg bg-brand px-4 py-2 text-sm font-medium text-brand-fg hover:opacity-90 disabled:opacity-50">
          <Save size={15} /> {save.isPending ? 'Saving…' : 'Save settings'}
        </button>
      </div>
    </div>
  );
}
