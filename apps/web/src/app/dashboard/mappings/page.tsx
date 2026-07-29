'use client';

import { useState } from 'react';
import {
  ArrowLeftRight, Link2, AlertTriangle, RefreshCw, ShieldCheck, CheckCircle2, ChevronDown,
  FileText, Plus, Pencil, Trash2, Info, ListChecks, Flag, LayoutGrid, FolderClosed,
} from 'lucide-react';

type Row = { id: string; portal: string; tone: string; psa: string };

const TABS = [
  { key: 'status', label: 'Status', icon: ListChecks },
  { key: 'priority', label: 'Priority', icon: Flag },
  { key: 'queue', label: 'Queue / Board', icon: LayoutGrid },
  { key: 'category', label: 'Category', icon: FolderClosed },
] as const;
type TabKey = (typeof TABS)[number]['key'];

const PSA_OPTIONS: Record<TabKey, string[]> = {
  status: ['New (Not Responded)', 'In Progress', 'Waiting on Customer', 'On Hold', 'Resolved', 'Closed', 'Scheduled', 'Escalated'],
  priority: ['Priority 1 - Emergency', 'Priority 2 - High', 'Priority 3 - Medium', 'Priority 4 - Low', 'No SLA'],
  queue: ['Service Desk', 'Network Operations', 'Professional Services', 'Triage', 'Onboarding'],
  category: ['Hardware', 'Software', 'Network', 'Account / Access', 'Email', 'Security'],
};

const INITIAL: Record<TabKey, Row[]> = {
  status: [
    { id: 's1', portal: 'New', tone: 'blue', psa: 'New (Not Responded)' },
    { id: 's2', portal: 'In Progress', tone: 'amber', psa: 'In Progress' },
    { id: 's3', portal: 'Waiting Customer', tone: 'violet', psa: 'Waiting on Customer' },
    { id: 's4', portal: 'On Hold', tone: 'orange', psa: 'On Hold' },
    { id: 's5', portal: 'Resolved', tone: 'green', psa: 'Resolved' },
    { id: 's6', portal: 'Closed', tone: 'slate', psa: 'Closed' },
  ],
  priority: [
    { id: 'p1', portal: 'Critical', tone: 'red', psa: 'Priority 1 - Emergency' },
    { id: 'p2', portal: 'High', tone: 'orange', psa: 'Priority 2 - High' },
    { id: 'p3', portal: 'Normal', tone: 'blue', psa: 'Priority 3 - Medium' },
    { id: 'p4', portal: 'Low', tone: 'green', psa: 'Priority 4 - Low' },
  ],
  queue: [
    { id: 'q1', portal: 'Help Desk', tone: 'blue', psa: 'Service Desk' },
    { id: 'q2', portal: 'Network', tone: 'violet', psa: 'Network Operations' },
    { id: 'q3', portal: 'Projects', tone: 'green', psa: 'Professional Services' },
  ],
  category: [
    { id: 'c1', portal: 'Hardware', tone: 'orange', psa: 'Hardware' },
    { id: 'c2', portal: 'Software', tone: 'blue', psa: 'Software' },
    { id: 'c3', portal: 'Network', tone: 'violet', psa: 'Network' },
    { id: 'c4', portal: 'Access', tone: 'green', psa: 'Account / Access' },
  ],
};

const badgeTone: Record<string, string> = {
  blue: 'bg-blue-50 text-blue-700 dark:bg-blue-950/60 dark:text-blue-300',
  amber: 'bg-amber-50 text-amber-700 dark:bg-amber-950/60 dark:text-amber-300',
  violet: 'bg-violet-50 text-violet-700 dark:bg-violet-950/60 dark:text-violet-300',
  orange: 'bg-orange-50 text-orange-700 dark:bg-orange-950/60 dark:text-orange-300',
  green: 'bg-green-50 text-green-700 dark:bg-green-950/60 dark:text-green-300',
  red: 'bg-red-50 text-red-700 dark:bg-red-950/60 dark:text-red-300',
  slate: 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300',
};

function StatCard({ icon: Icon, iconTone, label, value, sub, subTone }: {
  icon: React.ElementType; iconTone: string; label: string; value: React.ReactNode; sub: string; subTone?: string;
}) {
  return (
    <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-4">
      <div className="flex items-start gap-3">
        <span className={`inline-flex h-10 w-10 items-center justify-center rounded-lg ${iconTone}`}><Icon size={18} /></span>
        <div>
          <div className="text-xs text-[var(--muted)]">{label}</div>
          <div className="text-2xl font-semibold leading-tight">{value}</div>
          <div className={`mt-0.5 text-xs ${subTone ?? 'text-[var(--muted)]'}`}>{sub}</div>
        </div>
      </div>
    </div>
  );
}

export default function MappingsPage() {
  const [tab, setTab] = useState<TabKey>('status');
  const [data, setData] = useState<Record<TabKey, Row[]>>(INITIAL);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [savedAt, setSavedAt] = useState<string | null>(null);

  const rows = data[tab];
  const mapped = rows.filter((r) => r.psa).length;
  const options = PSA_OPTIONS[tab];
  const tabLabel = TABS.find((t) => t.key === tab)!.label;

  function update(id: string, patch: Partial<Row>) {
    setData((d) => ({ ...d, [tab]: d[tab].map((r) => (r.id === id ? { ...r, ...patch } : r)) }));
    setSavedAt('just now');
  }
  function remove(id: string) {
    setData((d) => ({ ...d, [tab]: d[tab].filter((r) => r.id !== id) }));
    setSavedAt('just now');
  }
  function addRow() {
    const id = `n${Date.now() % 100000}`;
    setData((d) => ({ ...d, [tab]: [...d[tab], { id, portal: 'New Value', tone: 'slate', psa: '' }] }));
    setEditingId(id);
  }

  return (
    <div className="space-y-5">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Field Mapping</h1>
          <p className="text-sm text-[var(--muted)]">Map the portal&apos;s neutral values to each PSA&apos;s real values — how your portal talks to the PSA.</p>
        </div>
        <a href="/user-guide.pdf" target="_blank" rel="noopener noreferrer"
          className="inline-flex shrink-0 items-center gap-2 rounded-lg border border-[var(--border)] bg-[var(--surface)] px-3 py-2 text-sm font-medium hover:bg-[var(--bg)]">
          <FileText size={15} /> Mapping Guide
        </a>
      </div>

      <div className="flex items-start gap-2 rounded-lg border border-blue-200 bg-blue-50 px-4 py-3 text-sm text-blue-800 dark:border-blue-900 dark:bg-blue-950/40 dark:text-blue-200">
        <Info size={16} className="mt-0.5 shrink-0" />
        <p>Field mapping ensures data consistency between Desk Portal and your connected PSA. <strong>Changes are saved automatically.</strong></p>
      </div>

      {/* Connection + tabs */}
      <div className="flex flex-wrap items-center gap-3">
        <div className="text-xs font-medium uppercase tracking-wide text-[var(--faint)]">Connection</div>
        <button className="inline-flex items-center gap-2 rounded-lg border border-[var(--border)] bg-[var(--surface)] px-3 py-2 text-sm">
          <span className="flex h-6 w-6 items-center justify-center rounded-full bg-blue-100 text-[10px] font-bold text-blue-700 dark:bg-blue-950 dark:text-blue-300">CW</span>
          ConnectWise Manage
          <span className="inline-flex items-center gap-1 rounded-full bg-green-100 px-2 py-0.5 text-xs font-medium text-green-700 dark:bg-green-950 dark:text-green-300"><CheckCircle2 size={11} /> Connected</span>
          <ChevronDown size={14} className="text-[var(--faint)]" />
        </button>
        <div className="ml-auto inline-flex flex-wrap rounded-lg border border-[var(--border)] bg-[var(--surface)] p-0.5">
          {TABS.map((t) => {
            const Icon = t.icon;
            return (
              <button key={t.key} onClick={() => { setTab(t.key); setEditingId(null); }}
                className={`inline-flex items-center gap-1.5 rounded-md px-3 py-1.5 text-sm font-medium ${tab === t.key ? 'bg-brand text-brand-fg' : 'text-[var(--muted)] hover:text-[var(--fg)]'}`}>
                <Icon size={14} /> {t.label}
              </button>
            );
          })}
        </div>
      </div>

      {/* Stat cards */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard icon={Link2} iconTone="bg-blue-50 text-blue-600 dark:bg-blue-950/50 dark:text-blue-300"
          label="Mapped Fields" value={<span>{mapped} <span className="text-[var(--faint)]">/ {rows.length}</span></span>}
          sub={mapped === rows.length ? `All ${tabLabel.toLowerCase()} fields mapped ✓` : `${rows.length - mapped} left to map`}
          subTone={mapped === rows.length ? 'text-green-600 dark:text-green-400' : 'text-amber-600 dark:text-amber-400'} />
        <StatCard icon={AlertTriangle} iconTone="bg-amber-50 text-amber-600 dark:bg-amber-950/50 dark:text-amber-300"
          label="Unmapped Fields" value={rows.length - mapped}
          sub={rows.length - mapped === 0 ? 'Everything is mapped ✓' : 'Needs attention'}
          subTone={rows.length - mapped === 0 ? 'text-green-600 dark:text-green-400' : 'text-amber-600 dark:text-amber-400'} />
        <StatCard icon={RefreshCw} iconTone="bg-emerald-50 text-emerald-600 dark:bg-emerald-950/50 dark:text-emerald-300"
          label="Last Synced" value={<span className="text-lg">{savedAt ?? '2 min ago'}</span>} sub="May 20, 2025 10:30 AM" />
        <StatCard icon={ShieldCheck} iconTone="bg-violet-50 text-violet-600 dark:bg-violet-950/50 dark:text-violet-300"
          label="Mapping Status" value={<span className="text-lg text-green-600 dark:text-green-400">Healthy</span>} sub="No issues detected" />
      </div>

      {/* Table */}
      <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)]">
        <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border)] px-5 py-3.5">
          <h2 className="text-sm font-semibold">{tabLabel} Field Mapping</h2>
          <div className="flex items-center gap-2">
            <button onClick={addRow} className="inline-flex items-center gap-1.5 text-sm font-medium text-brand hover:underline">
              <Plus size={15} /> Add Custom Mapping
            </button>
            <button onClick={() => setSavedAt('just now')} className="inline-flex items-center gap-1.5 rounded-lg border border-[var(--border)] px-2.5 py-1.5 text-sm font-medium text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)]">
              <RefreshCw size={14} /> Refresh
            </button>
          </div>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="text-left text-[10px] uppercase tracking-wide text-[var(--faint)]">
              <tr className="border-b border-[var(--border)]">
                <th className="px-5 py-2.5 font-medium">Portal {tabLabel} (Neutral)</th>
                <th className="px-2 py-2.5 text-center font-medium"><ArrowLeftRight size={13} className="mx-auto" /></th>
                <th className="px-5 py-2.5 font-medium">PSA {tabLabel} (ConnectWise Manage)</th>
                <th className="px-5 py-2.5 font-medium">Status</th>
                <th className="px-5 py-2.5 text-right font-medium">Actions</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.id} className="border-b border-[var(--border)] last:border-0">
                  <td className="px-5 py-3">
                    {editingId === r.id ? (
                      <input autoFocus value={r.portal} onChange={(e) => update(r.id, { portal: e.target.value })}
                        onBlur={() => setEditingId(null)} onKeyDown={(e) => e.key === 'Enter' && setEditingId(null)}
                        className="w-40 rounded-md border border-brand bg-[var(--bg)] px-2 py-1 text-sm outline-none" />
                    ) : (
                      <span className={`inline-flex rounded-md px-2.5 py-1 text-xs font-medium ${badgeTone[r.tone] ?? badgeTone.slate}`}>{r.portal}</span>
                    )}
                  </td>
                  <td className="px-2 py-3 text-center"><ArrowLeftRight size={14} className="mx-auto text-[var(--faint)]" /></td>
                  <td className="px-5 py-3">
                    <div className="relative max-w-xs">
                      <select value={r.psa} onChange={(e) => update(r.id, { psa: e.target.value })}
                        className="w-full appearance-none rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 pr-8 text-sm outline-none focus:border-brand">
                        <option value="">— not mapped —</option>
                        {options.map((o) => <option key={o} value={o}>{o}</option>)}
                      </select>
                      <ChevronDown size={14} className="pointer-events-none absolute right-2.5 top-1/2 -translate-y-1/2 text-[var(--faint)]" />
                    </div>
                  </td>
                  <td className="px-5 py-3">
                    {r.psa ? (
                      <span className="inline-flex items-center gap-1.5 text-sm font-medium text-green-600 dark:text-green-400"><CheckCircle2 size={15} /> Mapped</span>
                    ) : (
                      <span className="inline-flex items-center gap-1.5 text-sm font-medium text-amber-600 dark:text-amber-400"><AlertTriangle size={15} /> Unmapped</span>
                    )}
                  </td>
                  <td className="px-5 py-3">
                    <div className="flex items-center justify-end gap-1">
                      <button onClick={() => setEditingId(r.id)} aria-label="Edit"
                        className="rounded-md p-1.5 text-[var(--muted)] hover:bg-[var(--bg)] hover:text-brand"><Pencil size={15} /></button>
                      <button onClick={() => remove(r.id)} aria-label="Delete"
                        className="rounded-md p-1.5 text-[var(--muted)] hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-950/50"><Trash2 size={15} /></button>
                    </div>
                  </td>
                </tr>
              ))}
              {rows.length === 0 && (
                <tr><td colSpan={5} className="px-5 py-8 text-center text-sm text-[var(--muted)]">No mappings yet — use “Add Custom Mapping”.</td></tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
