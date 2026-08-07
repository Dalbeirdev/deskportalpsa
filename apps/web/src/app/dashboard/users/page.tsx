'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { UserPlus, Check, X, ShieldCheck, MailQuestion, Power } from 'lucide-react';
import { api, type StaffUser, type RoleOption } from '@/lib/api';

/**
 * The MSP's own people: technicians, managers, administrators, auditors.
 *
 * Creating a user here is an INVITATION, not a credential: sign-in stays with the identity
 * provider, and the account binds to it the first time the person logs in with a token whose
 * verified email matches. The list says which state each account is in, because "added but never
 * logged in" and "active daily user" look identical otherwise and only one of them can work.
 *
 * Role names render as stored ("MspAdministrator") in the API but as words here.
 */
const humanizeRole = (r: string) =>
  r.replace(/^Msp/, 'MSP ').replace(/([a-z])([A-Z])/g, '$1 $2').replace(/ +/g, ' ').trim();

function AddUserForm({ roles, onDone }: { roles: RoleOption[]; onDone: () => void }) {
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [picked, setPicked] = useState<Set<string>>(new Set());
  const [touched, setTouched] = useState(false);
  // Technician preselected: it is what this form exists for, and a safe least-privilege default.
  // Done in an effect, not the state initializer — roles arrive async, and an initializer that ran
  // against the empty first render left nothing selected and the submit permanently disabled.
  useEffect(() => {
    if (touched || picked.size > 0) return;
    const tech = roles.find((r) => r.name === 'Technician');
    if (tech) setPicked(new Set([tech.id]));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [roles]);

  const create = useMutation({
    mutationFn: () => api.createStaffUser({ displayName: name.trim(), email: email.trim(), roleIds: [...picked] }),
    onSuccess: () => { setName(''); setEmail(''); onDone(); },
  });

  const toggle = (id: string) => { setTouched(true); setPicked((p) => {
    const next = new Set(p);
    if (next.has(id)) next.delete(id); else next.add(id);
    return next;
  }); };

  return (
    <form onSubmit={(e) => { e.preventDefault(); create.mutate(); }}
      className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5">
      <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-[var(--faint)]">
        <UserPlus size={15} /> Add technician
      </h2>
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        <label className="block">
          <span className="mb-1 block text-xs font-medium">Full name</span>
          <input value={name} onChange={(e) => setName(e.target.value)} required minLength={2}
            className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm outline-none focus:border-brand" />
        </label>
        <label className="block">
          <span className="mb-1 block text-xs font-medium">Email</span>
          <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required
            className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm outline-none focus:border-brand" />
          <span className="mt-1 block text-xs text-[var(--muted)]">
            Must match the email in your identity provider — it is how their sign-in links up.
          </span>
        </label>
        <div>
          <span className="mb-1 block text-xs font-medium">Roles</span>
          <div className="flex flex-wrap gap-1.5">
            {roles.map((r) => (
              <button key={r.id} type="button" onClick={() => toggle(r.id)}
                className={`rounded-full border px-2.5 py-1 text-xs font-medium ${picked.has(r.id)
                  ? 'border-brand bg-brand/10 text-brand'
                  : 'border-[var(--border)] text-[var(--muted)] hover:bg-[var(--bg)]'}`}>
                {humanizeRole(r.name)}
              </button>
            ))}
          </div>
        </div>
      </div>
      <div className="mt-3 flex items-center gap-3">
        <button type="submit" disabled={create.isPending || picked.size === 0 || !name.trim() || !email.trim()}
          className="inline-flex items-center gap-2 rounded-lg bg-brand px-4 py-2 text-sm font-medium text-brand-fg hover:opacity-90 disabled:opacity-50">
          <UserPlus size={15} /> {create.isPending ? 'Adding…' : 'Add user'}
        </button>
        {create.isError && <span className="text-xs text-red-600 dark:text-red-400">
          {create.error instanceof Error ? create.error.message : 'Could not add the user.'}</span>}
        {create.isSuccess && <span className="inline-flex items-center gap-1 text-xs text-green-700 dark:text-green-400">
          <Check size={13} /> Added — they can sign in with their identity-provider account.</span>}
      </div>
    </form>
  );
}

function UserRow({ u, roles }: { u: StaffUser; roles: RoleOption[] }) {
  const qc = useQueryClient();
  const refresh = () => qc.invalidateQueries({ queryKey: ['staff-users'] });
  const setActive = useMutation({ mutationFn: (active: boolean) => api.setUserActive(u.id, active), onSuccess: refresh });
  const addRole = useMutation({ mutationFn: (roleId: string) => api.assignUserRole(u.id, roleId), onSuccess: refresh });
  const dropRole = useMutation({ mutationFn: (roleId: string) => api.removeUserRole(u.id, roleId), onSuccess: refresh });
  const held = new Set(u.roles.map((r) => r.id));
  const addable = roles.filter((r) => !held.has(r.id));

  return (
    <li className={`px-5 py-3 ${u.isActive ? '' : 'opacity-60'}`}>
      <div className="flex flex-wrap items-center gap-3">
        <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-brand/10 text-xs font-semibold text-brand">
          {u.displayName.slice(0, 1).toUpperCase()}
        </span>
        <span className="min-w-0">
          <span className="block truncate text-sm font-medium">{u.displayName}</span>
          <span className="block truncate text-xs text-[var(--muted)]">{u.email}</span>
        </span>
        {!u.signInLinked && (
          <span title="Created here, but they have not signed in yet. Their account links on first login by email."
            className="inline-flex shrink-0 items-center gap-1 rounded bg-amber-100 px-1.5 py-0.5 text-[11px] font-medium text-amber-700 dark:bg-amber-950 dark:text-amber-300">
            <MailQuestion size={11} /> Awaiting first sign-in
          </span>
        )}
        {!u.isActive && (
          <span className="shrink-0 rounded bg-slate-200 px-1.5 py-0.5 text-[11px] font-medium text-slate-600 dark:bg-slate-800 dark:text-slate-300">
            Deactivated
          </span>
        )}
        <span className="min-w-0 flex-1" />
        <span className="flex flex-wrap items-center gap-1.5">
          {u.roles.map((r) => (
            <span key={r.id} className="inline-flex items-center gap-1 rounded-full border border-[var(--border)] bg-[var(--bg)] px-2 py-0.5 text-[11px] font-medium">
              <ShieldCheck size={11} className="text-brand" /> {humanizeRole(r.name)}
              {u.roles.length > 1 && (
                <button onClick={() => dropRole.mutate(r.id)} aria-label={`Remove ${humanizeRole(r.name)} role`}
                  className="text-[var(--muted)] hover:text-red-600"><X size={11} /></button>
              )}
            </span>
          ))}
          {addable.length > 0 && (
            <select value="" onChange={(e) => { if (e.target.value) addRole.mutate(e.target.value); }}
              aria-label="Add role" className="rounded-full border border-dashed border-[var(--border)] bg-transparent px-2 py-0.5 text-[11px] text-[var(--muted)]">
              <option value="">+ role</option>
              {addable.map((r) => <option key={r.id} value={r.id}>{humanizeRole(r.name)}</option>)}
            </select>
          )}
        </span>
        <button onClick={() => setActive.mutate(!u.isActive)} disabled={setActive.isPending}
          title={u.isActive ? 'Deactivate — blocks sign-in until reactivated' : 'Reactivate'}
          className={`inline-flex shrink-0 items-center gap-1.5 rounded-lg border px-2.5 py-1.5 text-xs font-medium ${u.isActive
            ? 'border-[var(--border)] text-[var(--muted)] hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-950/40'
            : 'border-green-300 text-green-700 hover:bg-green-50 dark:text-green-400 dark:hover:bg-green-950/40'}`}>
          <Power size={13} /> {u.isActive ? 'Deactivate' : 'Reactivate'}
        </button>
      </div>
      {setActive.isError && (
        <p className="mt-1 text-xs text-red-600 dark:text-red-400">
          {setActive.error instanceof Error ? setActive.error.message : 'Could not change the account state.'}
        </p>
      )}
    </li>
  );
}

export default function UsersPage() {
  const qc = useQueryClient();
  const { data: users, isLoading } = useQuery({ queryKey: ['staff-users'], queryFn: api.staffUsers });
  const { data: roles } = useQuery({ queryKey: ['staff-roles'], queryFn: api.staffRoles, staleTime: 5 * 60_000 });

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-semibold">Users</h1>
        <p className="text-sm text-[var(--muted)]">
          Your team&apos;s portal accounts. Sign-in itself stays with your identity provider.
        </p>
      </div>

      <AddUserForm roles={roles ?? []} onDone={() => qc.invalidateQueries({ queryKey: ['staff-users'] })} />

      <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)]">
        <div className="border-b border-[var(--border)] px-5 py-3">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-[var(--faint)]">
            Team <span className="rounded-full bg-[var(--bg)] px-2 py-0.5 text-xs text-[var(--muted)]">{users?.length ?? 0}</span>
          </h2>
        </div>
        {isLoading ? (
          <p className="px-5 py-4 text-sm text-[var(--muted)]">Loading…</p>
        ) : (
          <ul className="divide-y divide-[var(--border)]">
            {(users ?? []).map((u) => <UserRow key={u.id} u={u} roles={roles ?? []} />)}
            {users?.length === 0 && <li className="px-5 py-4 text-sm text-[var(--muted)]">No staff accounts yet.</li>}
          </ul>
        )}
      </div>
    </div>
  );
}
