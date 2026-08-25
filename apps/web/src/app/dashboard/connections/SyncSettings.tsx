'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { RefreshCw, Filter, Save, CheckCircle2, AlertTriangle, Settings2, Clock } from 'lucide-react';
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

  const check = useMutation({ mutationFn: () => api.checkTimeEntry(connectionId) });

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
          <Toggle label="Sync attachments" checked={form.syncAttachments} onChange={(v) => set('syncAttachments', v)}
            hint="Mirror files both ways. Uploads here are pushed to the PSA; provider files are pulled in and virus-scanned." />
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

      <div>
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h3 className="flex items-center gap-2 text-sm font-semibold"><Clock size={15} className="text-brand" /> Time entry defaults</h3>
          {/* Answers "will time logging actually work?" without writing an entry — the question
              that previously could only be answered by losing a technician's hour to a rejection. */}
          <button type="button" onClick={() => check.mutate()} disabled={check.isPending}
            className="inline-flex items-center gap-1.5 rounded-lg border border-[var(--border)] px-3 py-1.5 text-xs font-medium hover:bg-[var(--bg)] disabled:opacity-50">
            <CheckCircle2 size={13} /> {check.isPending ? 'Checking…' : 'Check time logging'}
          </button>
        </div>
        {check.data && (
          <div className={`mt-2 rounded-lg border px-3 py-2 text-xs ${check.data.ready
            ? 'border-green-200 bg-green-50 text-green-800 dark:border-green-900/60 dark:bg-green-950/30 dark:text-green-300'
            : 'border-amber-200 bg-amber-50 text-amber-800 dark:border-amber-900/60 dark:bg-amber-950/30 dark:text-amber-300'}`}>
            <p className="font-medium">{check.data.summary}</p>
            {(check.data.remedies?.length ?? 0) > 0 && (
              <ul className="mt-1 list-disc space-y-0.5 pl-4">
                {check.data.remedies!.map((r) => <li key={r}>{r}</li>)}
              </ul>
            )}
          </div>
        )}
        {check.isError && (
          <p className="mt-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700 dark:border-red-900/60 dark:bg-red-950/30 dark:text-red-300">
            {(check.error as Error)?.message ?? 'Check failed.'}
          </p>
        )}
        <p className="text-xs text-[var(--muted)]">
          Who portal-logged time belongs to in the PSA.{' '}
          {provider === 2 && <>Autotask requires a technician and a work role on every ticket time entry, and rejects
          its own API user — so time logging stays disabled until a technician is chosen here.</>}
        </p>
        <div className="mt-2 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <label className="block">
            <span className="mb-1 block text-xs font-medium">Time entry {provider === 1 ? 'member' : 'technician'}</span>
            <select value={form.defaultTimeEntryResourceId ?? ''} onChange={(e) => set('defaultTimeEntryResourceId', e.target.value || null)}
              className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm outline-none focus:border-brand">
              <option value="">— none —</option>
              {(() => {
                // Autotask can only accept time from a technician who holds a work role, and the
                // list gives no clue which ones do — so "Autotask Administrator" looks like a
                // reasonable pick and rejects every entry. Say it in the option itself, and put
                // the usable people first, so the valid choice is the obvious one.
                const list = fields?.technicians ?? [];
                if (provider !== 2 || !(fields?.technicianCoverage?.length)) {
                  return list.map((o) => <option key={o.value} value={o.value}>{o.label}</option>);
                }
                const withRole = new Set(fields!.technicianCoverage.filter((c) => c.roleId).map((c) => c.technicianId));
                const usable = list.filter((o) => withRole.has(o.value));
                const unusable = list.filter((o) => !withRole.has(o.value));
                return [
                  ...usable.map((o) => <option key={o.value} value={o.value}>{o.label}</option>),
                  ...unusable.map((o) => (
                    <option key={o.value} value={o.value}>{o.label} — no work role, cannot log time</option>
                  )),
                ];
              })()}
            </select>
            {provider === 2 && !form.defaultTimeEntryResourceId &&
              <span className="mt-1 block text-xs text-amber-600 dark:text-amber-400">Required before time can be logged.</span>}
            {provider === 2 &&
              <span className="mt-1 block text-xs text-[var(--faint)]">
                Pick a real technician, not the API user — Autotask refuses to let its own integration
                account own time. Example: choose <em>Jane Cooper</em> (an engineer who works tickets).
              </span>}
          </label>
          {(() => {
            // Autotask accepts a time entry only when the technician ACTUALLY HOLDS the role.
            // Discovery already knows every real pairing, so offer only those: an impossible
            // combination stops being something an admin can pick, which is the only fix that
            // holds — validating after the fact just moves the error later.
            const held = (fields?.technicianCoverage ?? [])
              .filter((c) => c.technicianId === form.defaultTimeEntryResourceId && c.roleId)
              .map((c) => ({ value: c.roleId!, label: c.roleName ?? c.roleId! }));
            const seen = new Set<string>();
            const heldUnique = held.filter((r) => !seen.has(r.value) && seen.add(r.value));
            // Only Autotask enforces the pairing; ConnectWise takes any work role.
            const restrict = provider === 2 && !!form.defaultTimeEntryResourceId && heldUnique.length > 0;
            const options = restrict ? heldUnique : (fields?.workRoles ?? []);
            const chosenIsImpossible = provider === 2 && !!form.defaultTimeEntryRoleId
              && heldUnique.length > 0 && !heldUnique.some((r) => r.value === form.defaultTimeEntryRoleId);
            const technicianHasNoRoles = provider === 2 && !!form.defaultTimeEntryResourceId
              && (fields?.technicianCoverage?.length ?? 0) > 0 && heldUnique.length === 0;
            return (
          <label className="block">
            <span className="mb-1 block text-xs font-medium">Default work role</span>
            <select value={form.defaultTimeEntryRoleId ?? ''} onChange={(e) => set('defaultTimeEntryRoleId', e.target.value || null)}
              className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm outline-none focus:border-brand">
              <option value="">— the technician&apos;s own role —</option>
              {options.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
            </select>
            {chosenIsImpossible && (
              <span className="mt-1 block text-xs text-amber-600 dark:text-amber-400">
                The saved role is not one this technician holds — Autotask would reject it. Pick one from the list, or clear it.
              </span>
            )}
            {technicianHasNoRoles && (
              <span className="mt-1 block text-xs text-amber-600 dark:text-amber-400">
                This technician holds no active work role in Autotask, so they cannot own time. Give them a role there, or choose someone else.
              </span>
            )}
            {restrict && (
              <span className="mt-1 block text-[11px] text-[var(--faint)]">Showing only roles this technician holds.</span>
            )}
            <span className="mt-1 block text-xs text-[var(--muted)]">
              Leave unset to use whichever role the technician holds — the safest choice.
              {provider === 2 && ' Autotask only accepts a role that technician actually holds; if you pick one they do not, we use a valid one instead so the time is never lost.'}
            </span>
          </label>
            );
          })()}
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
