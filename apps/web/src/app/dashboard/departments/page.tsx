'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ChevronDown, ChevronRight, Plus, Pencil, Trash2, Power, Users } from 'lucide-react';
import { api, type DepartmentManage, type TeamManage } from '@/lib/api';

/**
 * The staff org structure Users & Access Management assigns people to — separate from that
 * feature's own pages, which only ever picked from what already existed here. Departments/Teams
 * are tenant-owned and fully editable, not a fixed catalogue: the 7 seeded defaults are a
 * starting point.
 *
 * Delete is a hard delete (cascades to teams and every user's membership) — Deactivate is the
 * safer default for "we don't use this anymore" since it just hides the row from pickers
 * elsewhere without touching anyone's existing assignment.
 */

function AddDepartmentForm({ onClose, onCreated }: { onClose: () => void; onCreated: () => void }) {
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const create = useMutation({
    mutationFn: () => api.createDepartment({ name: name.trim(), description: description.trim() || null }),
    onSuccess: onCreated,
  });

  return (
    <form onSubmit={(e) => { e.preventDefault(); create.mutate(); }}
      className="flex flex-wrap items-end gap-3 rounded-xl border border-[var(--border)] bg-[var(--surface)] p-4">
      <label className="block">
        <span className="mb-1 block text-xs font-medium">Name *</span>
        <input value={name} onChange={(e) => setName(e.target.value)} required minLength={2} autoFocus
          className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm outline-none focus:border-brand" />
      </label>
      <label className="block min-w-[200px] flex-1">
        <span className="mb-1 block text-xs font-medium">Description</span>
        <input value={description} onChange={(e) => setDescription(e.target.value)}
          className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm outline-none focus:border-brand" />
      </label>
      <button type="submit" disabled={name.trim().length < 2 || create.isPending}
        className="rounded-lg bg-brand px-4 py-2 text-sm font-medium text-brand-fg hover:opacity-90 disabled:opacity-50">
        {create.isPending ? 'Adding…' : 'Add'}
      </button>
      <button type="button" onClick={onClose}
        className="rounded-lg border border-[var(--border)] px-3 py-2 text-sm font-medium hover:bg-[var(--bg)]">
        Cancel
      </button>
      {create.isError && <span className="w-full text-xs text-red-600 dark:text-red-400">
        {create.error instanceof Error ? create.error.message : 'Could not add the department.'}</span>}
    </form>
  );
}

function AddTeamForm({ departmentId, onClose, onCreated }: { departmentId: string; onClose: () => void; onCreated: () => void }) {
  const [name, setName] = useState('');
  const create = useMutation({
    mutationFn: () => api.createTeam({ departmentId, name: name.trim() }),
    onSuccess: onCreated,
  });

  return (
    <form onSubmit={(e) => { e.preventDefault(); create.mutate(); }} className="mt-2 flex flex-wrap items-center gap-2">
      <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Team name" required minLength={2} autoFocus
        className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-2 py-1.5 text-xs outline-none focus:border-brand" />
      <button type="submit" disabled={name.trim().length < 2 || create.isPending}
        className="rounded-lg bg-brand px-3 py-1.5 text-xs font-medium text-brand-fg hover:opacity-90 disabled:opacity-50">
        {create.isPending ? 'Adding…' : 'Add'}
      </button>
      <button type="button" onClick={onClose} className="text-xs text-[var(--muted)] hover:text-[var(--fg)]">Cancel</button>
      {create.isError && <span className="w-full text-[11px] text-red-600 dark:text-red-400">
        {create.error instanceof Error ? create.error.message : 'Could not add the team.'}</span>}
    </form>
  );
}

function TeamRow({ team, onChanged }: { team: TeamManage; onChanged: () => void }) {
  const [editing, setEditing] = useState(false);
  const [name, setName] = useState(team.name);
  const [error, setError] = useState<string | null>(null);

  const save = useMutation({
    mutationFn: () => api.updateTeam(team.id, { name: name.trim() }),
    onSuccess: () => { onChanged(); setEditing(false); setError(null); },
    onError: (e) => setError(e instanceof Error ? e.message : 'Could not save.'),
  });
  const toggleActive = useMutation({ mutationFn: () => api.setTeamActive(team.id, !team.isActive), onSuccess: onChanged });
  const del = useMutation({
    mutationFn: () => api.deleteTeam(team.id),
    onSuccess: () => { onChanged(); setError(null); },
    onError: (e) => setError(e instanceof Error ? e.message : 'Could not delete this team.'),
  });

  return (
    <li className={`flex flex-wrap items-center gap-2 rounded-lg px-2 py-1.5 ${team.isActive ? '' : 'opacity-60'}`}>
      {editing ? (
        <>
          <input value={name} onChange={(e) => setName(e.target.value)} autoFocus
            className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-2 py-1 text-xs outline-none focus:border-brand" />
          <button onClick={() => save.mutate()} disabled={save.isPending} className="text-xs font-medium text-brand hover:underline">Save</button>
          <button onClick={() => { setEditing(false); setName(team.name); setError(null); }} className="text-xs text-[var(--muted)] hover:text-[var(--fg)]">Cancel</button>
        </>
      ) : (
        <>
          <span className="flex-1 text-sm">{team.name}</span>
          {!team.isActive && <span className="rounded bg-slate-200 px-1.5 py-0.5 text-[10px] font-medium text-slate-600 dark:bg-slate-800 dark:text-slate-300">Inactive</span>}
          <span className="flex items-center gap-1 text-xs text-[var(--muted)]"><Users size={11} /> {team.userCount}</span>
          <button onClick={() => setEditing(true)} aria-label={`Edit ${team.name}`} className="rounded p-1 text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)]"><Pencil size={12} /></button>
          <button onClick={() => toggleActive.mutate()} aria-label={team.isActive ? `Deactivate ${team.name}` : `Reactivate ${team.name}`}
            className="rounded p-1 text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)]"><Power size={12} /></button>
          <button
            onClick={() => {
              const impact = team.userCount > 0 ? ` ${team.userCount} user${team.userCount === 1 ? '' : 's'} will lose this team.` : '';
              if (window.confirm(`Delete "${team.name}"?${impact} This cannot be undone.`)) del.mutate();
            }}
            aria-label={`Delete ${team.name}`} className="rounded p-1 text-[var(--muted)] hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-950/40"><Trash2 size={12} /></button>
        </>
      )}
      {error && <span className="w-full text-[11px] text-red-600 dark:text-red-400">{error}</span>}
    </li>
  );
}

function DepartmentRow({ dept, open, onToggle, onChanged }: {
  dept: DepartmentManage; open: boolean; onToggle: () => void; onChanged: () => void;
}) {
  const [editing, setEditing] = useState(false);
  const [name, setName] = useState(dept.name);
  const [description, setDescription] = useState(dept.description ?? '');
  const [showAddTeam, setShowAddTeam] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const save = useMutation({
    mutationFn: () => api.updateDepartment(dept.id, { name: name.trim(), description: description.trim() || null }),
    onSuccess: () => { onChanged(); setEditing(false); setError(null); },
    onError: (e) => setError(e instanceof Error ? e.message : 'Could not save.'),
  });
  const toggleActive = useMutation({ mutationFn: () => api.setDepartmentActive(dept.id, !dept.isActive), onSuccess: onChanged });
  const del = useMutation({
    mutationFn: () => api.deleteDepartment(dept.id),
    onSuccess: () => { onChanged(); setError(null); },
    onError: (e) => setError(e instanceof Error ? e.message : 'Could not delete this department.'),
  });

  const totalUsers = dept.primaryUserCount + dept.secondaryUserCount;

  const confirmDelete = () => {
    const parts: string[] = [];
    if (dept.teams.length > 0) parts.push(`${dept.teams.length} team${dept.teams.length === 1 ? '' : 's'}`);
    if (totalUsers > 0) parts.push(`${totalUsers} user assignment${totalUsers === 1 ? '' : 's'}`);
    const impact = parts.length > 0 ? ` This removes ${parts.join(' and ')}.` : '';
    if (window.confirm(`Delete "${dept.name}"?${impact} This cannot be undone.`)) del.mutate();
  };

  return (
    <div className={`rounded-xl border border-[var(--border)] bg-[var(--surface)] ${dept.isActive ? '' : 'opacity-60'}`}>
      <div className="flex flex-wrap items-center gap-3 px-4 py-3">
        <button onClick={onToggle} aria-label={open ? `Collapse ${dept.name}` : `Expand ${dept.name}`} className="text-[var(--muted)] hover:text-[var(--fg)]">
          {open ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
        </button>
        {editing ? (
          <div className="flex flex-1 flex-wrap items-center gap-2">
            <input value={name} onChange={(e) => setName(e.target.value)} autoFocus
              className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-2 py-1 text-sm outline-none focus:border-brand" />
            <input value={description} onChange={(e) => setDescription(e.target.value)} placeholder="Description"
              className="min-w-[160px] flex-1 rounded-lg border border-[var(--border)] bg-[var(--bg)] px-2 py-1 text-sm outline-none focus:border-brand" />
            <button onClick={() => save.mutate()} disabled={save.isPending} className="rounded-lg border border-[var(--border)] px-2 py-1 text-xs font-medium hover:bg-[var(--bg)]">Save</button>
            <button onClick={() => { setEditing(false); setName(dept.name); setDescription(dept.description ?? ''); setError(null); }} className="text-xs text-[var(--muted)] hover:text-[var(--fg)]">Cancel</button>
          </div>
        ) : (
          <div className="min-w-0 flex-1">
            <span className="flex items-center gap-2">
              <span className="font-medium">{dept.name}</span>
              {!dept.isActive && <span className="rounded bg-slate-200 px-1.5 py-0.5 text-[10px] font-medium text-slate-600 dark:bg-slate-800 dark:text-slate-300">Inactive</span>}
              {dept.isSystemDefault && <span className="text-[10px] text-[var(--faint)]">Default</span>}
            </span>
            {dept.description && <span className="block truncate text-xs text-[var(--muted)]">{dept.description}</span>}
          </div>
        )}
        <span className="flex items-center gap-1 text-xs text-[var(--muted)]"><Users size={12} /> {totalUsers}</span>
        <span className="text-xs text-[var(--muted)]">{dept.teams.length} team{dept.teams.length === 1 ? '' : 's'}</span>
        {!editing && (
          <div className="flex items-center gap-1">
            <button onClick={() => setEditing(true)} aria-label={`Edit ${dept.name}`} className="rounded-lg p-1.5 text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)]"><Pencil size={14} /></button>
            <button onClick={() => toggleActive.mutate()} aria-label={dept.isActive ? `Deactivate ${dept.name}` : `Reactivate ${dept.name}`}
              className="rounded-lg p-1.5 text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)]"><Power size={14} /></button>
            <button onClick={confirmDelete} aria-label={`Delete ${dept.name}`} className="rounded-lg p-1.5 text-[var(--muted)] hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-950/40"><Trash2 size={14} /></button>
          </div>
        )}
      </div>
      {error && <p className="px-4 pb-2 text-xs text-red-600 dark:text-red-400">{error}</p>}

      {open && (
        <div className="border-t border-[var(--border)] px-4 py-3">
          <ul className="space-y-1.5">
            {dept.teams.map((t) => <TeamRow key={t.id} team={t} onChanged={onChanged} />)}
            {dept.teams.length === 0 && !showAddTeam && <li className="text-xs text-[var(--faint)]">No teams yet.</li>}
          </ul>
          {showAddTeam ? (
            <AddTeamForm departmentId={dept.id} onClose={() => setShowAddTeam(false)} onCreated={() => { onChanged(); setShowAddTeam(false); }} />
          ) : (
            <button onClick={() => setShowAddTeam(true)} className="mt-2 inline-flex items-center gap-1.5 text-xs font-medium text-brand hover:underline">
              <Plus size={13} /> Add team
            </button>
          )}
        </div>
      )}
    </div>
  );
}

export default function DepartmentsPage() {
  const qc = useQueryClient();
  const { data, isLoading, isError } = useQuery({ queryKey: ['org-structure'], queryFn: api.orgStructure });
  const refresh = () => qc.invalidateQueries({ queryKey: ['org-structure'] });

  const [openId, setOpenId] = useState<string | null>(null);
  const [showAddDept, setShowAddDept] = useState(false);

  const departments = data ?? [];

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">Departments & Teams</h1>
          <p className="text-sm text-[var(--muted)]">
            Your staff org structure — what Users &amp; Access Management assigns people to.
          </p>
        </div>
        <button onClick={() => setShowAddDept((v) => !v)}
          className="inline-flex items-center gap-2 rounded-lg bg-brand px-3.5 py-2 text-sm font-medium text-brand-fg hover:opacity-90">
          <Plus size={16} /> Add department
        </button>
      </div>

      {showAddDept && (
        <AddDepartmentForm onClose={() => setShowAddDept(false)} onCreated={() => { refresh(); setShowAddDept(false); }} />
      )}

      {isLoading && <p className="px-1 text-sm text-[var(--muted)]">Loading…</p>}

      {isError && (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] px-5 py-8 text-center">
          <p className="text-sm font-medium">Could not load departments.</p>
        </div>
      )}

      {!isLoading && !isError && departments.length === 0 && (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] px-5 py-8 text-center">
          <p className="text-sm font-medium">No departments yet.</p>
          <p className="mt-1 text-xs text-[var(--muted)]">Add your first one to start organizing staff.</p>
        </div>
      )}

      <div className="space-y-2">
        {departments.map((d) => (
          <DepartmentRow key={d.id} dept={d} open={openId === d.id} onToggle={() => setOpenId(openId === d.id ? null : d.id)} onChanged={refresh} />
        ))}
      </div>
    </div>
  );
}
