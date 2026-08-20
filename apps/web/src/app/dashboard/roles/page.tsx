'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, ShieldCheck, Trash2, Users, Copy, Lock } from 'lucide-react';
import { api, type PermissionDefinition, type RoleDetail } from '@/lib/api';

/**
 * Roles & Permissions (§6). Built-in roles are read-only — they are shared across every tenant, so
 * editing one here would silently change other organizations' access. Custom roles are this
 * tenant's own: full matrix editing, guarded so you cannot edit a role you yourself hold (that
 * would be editing your own permissions — the same line the per-user self-guards draw).
 */

const SCOPE_LABEL: Record<number, string> = {
  0: 'All', 10: 'Department', 20: 'Team', 30: 'Assigned', 40: 'Own', 50: 'Selected', 60: 'None',
};
const NOT_GRANTED = -1;

type Draft = { name: string; grants: Record<string, number> };

function draftFrom(role: RoleDetail | null): Draft {
  return {
    name: role?.name ?? '',
    grants: Object.fromEntries((role?.grants ?? []).map((g) => [g.permissionKey, g.scope])),
  };
}

function MatrixEditor({ catalog, draft, onChange, readOnly }: {
  catalog: PermissionDefinition[]; draft: Draft; onChange: (d: Draft) => void; readOnly: boolean;
}) {
  const byModule = useMemo(() => {
    const groups: Record<string, PermissionDefinition[]> = {};
    for (const def of catalog) (groups[def.module] ??= []).push(def);
    return groups;
  }, [catalog]);

  return (
    <div className="space-y-4">
      {Object.entries(byModule).map(([module, defs]) => (
        <div key={module}>
          <h4 className="mb-1.5 text-xs font-semibold text-[var(--fg)]">{module}</h4>
          <div className="overflow-hidden rounded-lg border border-[var(--border)]">
            <table className="w-full text-xs">
              <tbody>
                {defs.map((def) => {
                  const value = draft.grants[def.key] ?? NOT_GRANTED;
                  return (
                    <tr key={def.key} className="border-b border-[var(--border)] last:border-0">
                      <td className="px-3 py-2 font-medium">{def.displayName}</td>
                      <td className="w-44 px-3 py-1.5">
                        <select value={value} disabled={readOnly}
                          aria-label={`${def.displayName} scope`}
                          onChange={(e) => {
                            const v = Number(e.target.value);
                            const grants = { ...draft.grants };
                            if (v === NOT_GRANTED) delete grants[def.key]; else grants[def.key] = v;
                            onChange({ ...draft, grants });
                          }}
                          className={`w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-2 py-1 outline-none focus:border-brand disabled:opacity-60 ${value === NOT_GRANTED ? 'text-[var(--faint)]' : ''}`}>
                          <option value={NOT_GRANTED}>Not granted</option>
                          {def.supportedScopes.map((s) => (
                            <option key={s} value={s}>{SCOPE_LABEL[s] ?? s}</option>
                          ))}
                        </select>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      ))}
    </div>
  );
}

export default function RolesPage() {
  const qc = useQueryClient();
  const { data: catalog } = useQuery({ queryKey: ['roles-catalog'], queryFn: api.rolesCatalog, staleTime: 5 * 60_000 });
  const { data: roles, isLoading, isError } = useQuery({ queryKey: ['roles-detailed'], queryFn: api.rolesDetailed });
  const refresh = () => {
    qc.invalidateQueries({ queryKey: ['roles-detailed'] });
    qc.invalidateQueries({ queryKey: ['staff-roles'] }); // the Users page pickers list custom roles too
  };

  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [draft, setDraft] = useState<Draft>(draftFrom(null));
  const [error, setError] = useState<string | null>(null);

  const list = roles ?? [];
  const selected = creating ? null : list.find((r) => r.id === selectedId) ?? list[0] ?? null;
  // Keep the draft in sync with the selection without an effect: derive a key and reset on change.
  const [draftFor, setDraftFor] = useState<string | null>(null);
  const activeKey = creating ? '__new__' : selected?.id ?? null;
  if (activeKey !== draftFor) {
    setDraftFor(activeKey);
    setDraft(creating ? draft : draftFrom(selected));
    setError(null);
  }

  const readOnly = !creating && (selected?.isSystemRole || selected?.heldByCaller || false);
  const grantsArray = () => Object.entries(draft.grants).map(([permissionKey, scope]) => ({ permissionKey, scope }));

  const create = useMutation({
    mutationFn: () => api.createRole({ name: draft.name.trim(), grants: grantsArray() }),
    onSuccess: (r) => { refresh(); setCreating(false); setSelectedId(r.id); setError(null); },
    onError: (e) => setError(e instanceof Error ? e.message : 'Could not create the role.'),
  });
  const update = useMutation({
    mutationFn: () => api.updateRole(selected!.id, { name: draft.name.trim(), grants: grantsArray() }),
    onSuccess: () => { refresh(); setError(null); },
    onError: (e) => setError(e instanceof Error ? e.message : 'Could not save the role.'),
  });
  const del = useMutation({
    mutationFn: () => api.deleteRole(selected!.id),
    onSuccess: () => { refresh(); setSelectedId(null); setError(null); },
    onError: (e) => setError(e instanceof Error ? e.message : 'Could not delete the role.'),
  });

  const startCreate = (from?: RoleDetail) => {
    setCreating(true);
    setDraftFor('__new__');
    setDraft(from ? { name: `${from.name} (copy)`, grants: draftFrom(from).grants } : draftFrom(null));
    setError(null);
  };

  const canSave = draft.name.trim().length >= 2 && Object.keys(draft.grants).length > 0;

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">Roles &amp; Permissions</h1>
          <p className="text-sm text-[var(--muted)]">
            What each role can do, and how far it reaches. Built-in roles are read-only — create custom roles for anything different.
          </p>
        </div>
        <button onClick={() => startCreate()}
          className="inline-flex items-center gap-2 rounded-lg bg-brand px-3.5 py-2 text-sm font-medium text-brand-fg hover:opacity-90">
          <Plus size={16} /> New role
        </button>
      </div>

      {isLoading && <p className="px-1 text-sm text-[var(--muted)]">Loading…</p>}
      {isError && (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] px-5 py-8 text-center">
          <p className="text-sm font-medium">Could not load roles.</p>
        </div>
      )}

      {!isLoading && !isError && (
        <div className="grid gap-4 lg:grid-cols-[280px_1fr]">
          {/* Role list */}
          <div className="space-y-2">
            {list.map((r) => (
              <button key={r.id} onClick={() => { setCreating(false); setSelectedId(r.id); }}
                className={`w-full rounded-xl border px-4 py-3 text-left ${!creating && selected?.id === r.id
                  ? 'border-brand bg-brand/5' : 'border-[var(--border)] bg-[var(--surface)] hover:bg-[var(--bg)]'}`}>
                <span className="flex items-center gap-2">
                  <ShieldCheck size={14} className="shrink-0 text-brand" />
                  <span className="min-w-0 flex-1 truncate text-sm font-medium">{r.name}</span>
                  {r.isSystemRole && (
                    <span className="rounded bg-slate-200 px-1.5 py-0.5 text-[10px] font-medium text-slate-600 dark:bg-slate-800 dark:text-slate-300">
                      Built-in
                    </span>
                  )}
                </span>
                <span className="mt-1 flex items-center gap-3 text-xs text-[var(--muted)]">
                  <span className="inline-flex items-center gap-1"><Users size={11} /> {r.userCount}</span>
                  <span>{r.grants.length} permission{r.grants.length === 1 ? '' : 's'}</span>
                </span>
              </button>
            ))}
            {creating && (
              <div className="w-full rounded-xl border border-brand bg-brand/5 px-4 py-3 text-sm font-medium">
                New role…
              </div>
            )}
          </div>

          {/* Detail / editor */}
          <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5">
            {(creating || selected) && catalog ? (
              <div className="space-y-4">
                <div className="flex flex-wrap items-center gap-3">
                  {creating || !readOnly ? (
                    <input value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })}
                      placeholder="Role name" aria-label="Role name"
                      className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm font-medium outline-none focus:border-brand" />
                  ) : (
                    <h2 className="text-sm font-semibold">{selected!.name}</h2>
                  )}
                  <span className="min-w-0 flex-1" />
                  {!creating && selected && (
                    <button onClick={() => startCreate(selected)} title="Start a new custom role from this one's grants"
                      className="inline-flex items-center gap-1.5 rounded-lg border border-[var(--border)] px-2.5 py-1.5 text-xs font-medium hover:bg-[var(--bg)]">
                      <Copy size={13} /> Duplicate
                    </button>
                  )}
                  {!creating && selected && !selected.isSystemRole && !selected.heldByCaller && (
                    <>
                      <button onClick={() => update.mutate()} disabled={!canSave || update.isPending}
                        className="rounded-lg bg-brand px-3.5 py-1.5 text-xs font-medium text-brand-fg hover:opacity-90 disabled:opacity-50">
                        {update.isPending ? 'Saving…' : 'Save'}
                      </button>
                      <button
                        onClick={() => { if (window.confirm(`Delete the "${selected.name}" role? This cannot be undone.`)) del.mutate(); }}
                        disabled={del.isPending || selected.userCount > 0}
                        title={selected.userCount > 0 ? `${selected.userCount} user(s) still hold this role` : undefined}
                        className="inline-flex items-center gap-1.5 rounded-lg border border-red-300 px-2.5 py-1.5 text-xs font-medium text-red-600 hover:bg-red-50 disabled:opacity-50 dark:hover:bg-red-950/40">
                        <Trash2 size={13} /> Delete
                      </button>
                    </>
                  )}
                  {creating && (
                    <>
                      <button onClick={() => create.mutate()} disabled={!canSave || create.isPending}
                        className="rounded-lg bg-brand px-3.5 py-1.5 text-xs font-medium text-brand-fg hover:opacity-90 disabled:opacity-50">
                        {create.isPending ? 'Creating…' : 'Create role'}
                      </button>
                      <button onClick={() => { setCreating(false); setError(null); }}
                        className="rounded-lg border border-[var(--border)] px-2.5 py-1.5 text-xs font-medium hover:bg-[var(--bg)]">
                        Cancel
                      </button>
                    </>
                  )}
                </div>

                {!creating && selected?.isSystemRole && (
                  <p className="flex items-center gap-2 rounded-lg bg-[var(--bg)] px-3 py-2 text-xs text-[var(--muted)]">
                    <Lock size={13} /> Built-in role — read-only. Use Duplicate to start a custom role from these grants.
                  </p>
                )}
                {!creating && selected?.heldByCaller && !selected.isSystemRole && (
                  <p className="flex items-center gap-2 rounded-lg bg-amber-50 px-3 py-2 text-xs text-amber-700 dark:bg-amber-950/50 dark:text-amber-300">
                    <Lock size={13} /> You hold this role, so you cannot edit it — that would change your own permissions. Ask another administrator.
                  </p>
                )}

                <MatrixEditor catalog={catalog} draft={draft} onChange={setDraft} readOnly={readOnly} />

                {error && <p className="text-xs text-red-600 dark:text-red-400">{error}</p>}
              </div>
            ) : (
              <p className="text-sm text-[var(--muted)]">Select a role to see its permissions.</p>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
