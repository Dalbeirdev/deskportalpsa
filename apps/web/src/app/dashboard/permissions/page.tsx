'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { KeyRound, ShieldCheck, ShieldOff, Users } from 'lucide-react';
import { api, type PermissionDefinition } from '@/lib/api';

/**
 * Effective Permissions (§13): the org-wide answer to "who can do this?". Pick a permission, see
 * every staff user's RESOLVED access to it — the same engine enforcement consults, so overrides
 * and denies show exactly as they will behave, not what a role listing would suggest.
 */

const SCOPE_LABEL: Record<number, string> = {
  0: 'All', 10: 'Department', 20: 'Team', 30: 'Assigned', 40: 'Own', 50: 'Selected', 60: 'None',
};

const SOURCE_LABEL: Record<string, { label: string; tone: string }> = {
  NoGrant: { label: 'Not granted', tone: 'text-[var(--faint)]' },
  RoleGrant: { label: 'Via role', tone: 'text-[var(--fg)]' },
  OverrideGrant: { label: 'Override — granted', tone: 'text-green-700 dark:text-green-400' },
  OverrideDeny: { label: 'Override — denied', tone: 'text-red-600 dark:text-red-400' },
};

export default function EffectivePermissionsPage() {
  const { data: catalog, isLoading: catalogLoading } = useQuery({
    queryKey: ['roles-catalog'], queryFn: api.rolesCatalog, staleTime: 5 * 60_000,
  });
  const [selectedKey, setSelectedKey] = useState<string | null>(null);
  const [showAll, setShowAll] = useState(false);

  const byModule = useMemo(() => {
    const groups: Record<string, PermissionDefinition[]> = {};
    for (const def of catalog ?? []) (groups[def.module] ??= []).push(def);
    return groups;
  }, [catalog]);

  const selected = (catalog ?? []).find((d) => d.key === selectedKey) ?? null;
  const { data: holders, isLoading: holdersLoading } = useQuery({
    queryKey: ['effective-permission-holders', selectedKey],
    queryFn: () => api.effectivePermissionHolders(selectedKey!),
    enabled: !!selectedKey,
  });

  const rows = holders ?? [];
  const withAccess = rows.filter((r) => r.source !== 'NoGrant' && r.source !== 'OverrideDeny');
  const visible = showAll ? rows : withAccess;

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-xl font-semibold">Effective Permissions</h1>
        <p className="text-sm text-[var(--muted)]">
          Who can do what, as enforcement actually resolves it — roles combined, overrides applied.
        </p>
      </div>

      {catalogLoading && <p className="px-1 text-sm text-[var(--muted)]">Loading…</p>}

      {catalog && (
        <div className="grid gap-4 lg:grid-cols-[300px_1fr]">
          {/* Permission picker */}
          <div className="space-y-4">
            {Object.entries(byModule).map(([module, defs]) => (
              <div key={module}>
                <h4 className="mb-1.5 px-1 text-xs font-semibold uppercase tracking-wide text-[var(--faint)]">{module}</h4>
                <div className="space-y-1">
                  {defs.map((def) => (
                    <button key={def.key} onClick={() => setSelectedKey(def.key)}
                      className={`flex w-full items-center gap-2 rounded-lg border px-3 py-2 text-left text-sm ${selectedKey === def.key
                        ? 'border-brand bg-brand/5 font-medium' : 'border-[var(--border)] bg-[var(--surface)] hover:bg-[var(--bg)]'}`}>
                      <KeyRound size={13} className="shrink-0 text-[var(--faint)]" />
                      <span className="truncate">{def.displayName}</span>
                    </button>
                  ))}
                </div>
              </div>
            ))}
          </div>

          {/* Holders */}
          <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5">
            {!selected ? (
              <p className="text-sm text-[var(--muted)]">Pick a permission to see who holds it.</p>
            ) : (
              <div className="space-y-4">
                <div className="flex flex-wrap items-center gap-3">
                  <h2 className="text-sm font-semibold">{selected.displayName}</h2>
                  <span className="rounded bg-[var(--bg)] px-2 py-0.5 font-mono text-[11px] text-[var(--muted)]">{selected.key}</span>
                  <span className="min-w-0 flex-1" />
                  {holders && (
                    <span className="inline-flex items-center gap-1.5 text-xs text-[var(--muted)]">
                      <Users size={13} /> {withAccess.length} of {rows.length} user{rows.length === 1 ? '' : 's'} can
                    </span>
                  )}
                  <label className="flex items-center gap-1.5 text-xs text-[var(--muted)]">
                    <input type="checkbox" checked={showAll} onChange={(e) => setShowAll(e.target.checked)} />
                    Show users without access
                  </label>
                </div>

                {holdersLoading && <p className="text-sm text-[var(--muted)]">Resolving…</p>}

                {!holdersLoading && visible.length === 0 && (
                  <p className="text-sm text-[var(--muted)]">
                    {rows.length === 0 ? 'No staff users yet.' : 'Nobody has this permission. Tick "Show users without access" to see everyone.'}
                  </p>
                )}

                {!holdersLoading && visible.length > 0 && (
                  <div className="overflow-x-auto rounded-lg border border-[var(--border)]">
                    <table className="w-full text-sm">
                      <thead className="text-left text-xs uppercase tracking-wide text-[var(--muted)]">
                        <tr className="border-b border-[var(--border)]">
                          <th className="px-4 py-2.5 font-medium">User</th>
                          <th className="px-4 py-2.5 font-medium">Access</th>
                          <th className="px-4 py-2.5 font-medium">How</th>
                          {selected.isBoardAware && <th className="px-4 py-2.5 font-medium">Boards</th>}
                        </tr>
                      </thead>
                      <tbody>
                        {visible.map((r) => {
                          const src = SOURCE_LABEL[r.source] ?? { label: r.source, tone: 'text-[var(--muted)]' };
                          const denied = r.source === 'OverrideDeny' || r.source === 'NoGrant';
                          return (
                            <tr key={r.userId} className={`border-b border-[var(--border)] last:border-0 ${r.isActive ? '' : 'opacity-60'}`}>
                              <td className="px-4 py-2.5">
                                <div className="flex items-center gap-2.5">
                                  {r.photoUrl ? (
                                    // eslint-disable-next-line @next/next/no-img-element
                                    <img src={r.photoUrl} alt="" className="h-6 w-6 shrink-0 rounded-full object-cover" />
                                  ) : (
                                    <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-brand/10 text-[10px] font-semibold text-brand">
                                      {r.displayName.slice(0, 1).toUpperCase()}
                                    </span>
                                  )}
                                  <span className="min-w-0">
                                    <Link href={`/dashboard/users/${r.userId}`} className="block truncate font-medium hover:underline">{r.displayName}</Link>
                                    <span className="block truncate text-xs text-[var(--muted)]">{r.email}</span>
                                  </span>
                                  {!r.isActive && (
                                    <span className="rounded bg-slate-200 px-1.5 py-0.5 text-[10px] font-medium text-slate-600 dark:bg-slate-800 dark:text-slate-300">
                                      Deactivated
                                    </span>
                                  )}
                                </div>
                              </td>
                              <td className="px-4 py-2.5">
                                <span className={`inline-flex items-center gap-1.5 text-xs font-medium ${denied ? 'text-[var(--faint)]' : ''}`}>
                                  {denied ? <ShieldOff size={13} /> : <ShieldCheck size={13} className="text-brand" />}
                                  {denied ? '—' : SCOPE_LABEL[r.scope] ?? r.scope}
                                </span>
                              </td>
                              <td className={`px-4 py-2.5 text-xs ${src.tone}`}>
                                {src.label}
                                {r.viaRoles.length > 0 && (
                                  <span className="text-[var(--muted)]">
                                    {r.source === 'RoleGrant' ? ': ' : ' (role grant from: '}
                                    {r.viaRoles.join(', ')}
                                    {r.source === 'RoleGrant' ? '' : ')'}
                                  </span>
                                )}
                              </td>
                              {selected.isBoardAware && (
                                <td className="px-4 py-2.5 text-xs text-[var(--muted)]">{denied ? '—' : r.boardAccessMode}</td>
                              )}
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
