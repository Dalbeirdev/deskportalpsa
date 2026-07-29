'use client';

import { useState } from 'react';
import {
  RefreshCw, ChevronDown, Filter, Clock, Mail, AlertOctagon, CheckCircle2, Layers,
  Plus, ArrowRight, AlertTriangle,
} from 'lucide-react';

type Health = 'Healthy' | 'Warning' | 'Critical';

const FEATURED = {
  name: 'TechPio ConnectWise Staging', abbr: 'CW', color: 'bg-blue-100 text-blue-700 dark:bg-blue-950 dark:text-blue-300',
  status: 'Healthy' as Health, lastSync: '2 min ago', monitors: 2,
  pending: 2, deadLetter: 0, failed: 0, overall: 100,
};

const CONNS = [
  { name: 'TechPio ConnectWise Staging', abbr: 'CW', color: 'bg-blue-100 text-blue-700 dark:bg-blue-950 dark:text-blue-300', status: 'Healthy' as Health, lastSync: '2 min ago', pending: 2, failed: 0, health: 100 },
  { name: 'TechPio Autotask Staging', abbr: 'AT', color: 'bg-red-100 text-red-700 dark:bg-red-950 dark:text-red-300', status: 'Healthy' as Health, lastSync: '5 min ago', pending: 1, failed: 0, health: 100 },
  { name: 'TechPio ServiceNow', abbr: 'SN', color: 'bg-slate-200 text-slate-700 dark:bg-slate-700 dark:text-slate-200', status: 'Warning' as Health, lastSync: '18 min ago', pending: 5, failed: 1, health: 85 },
  { name: 'HaloPSA Production', abbr: 'H', color: 'bg-sky-100 text-sky-700 dark:bg-sky-950 dark:text-sky-300', status: 'Healthy' as Health, lastSync: '7 min ago', pending: 0, failed: 0, health: 100 },
  { name: 'BMS Staging', abbr: 'B', color: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300', status: 'Healthy' as Health, lastSync: '12 min ago', pending: 0, failed: 0, health: 100 },
];

const ACTIVITY = [
  { ok: 'ok', time: '2 min ago', title: 'TechPio ConnectWise Staging', sub: 'Synchronization completed successfully', detail: '124 records processed' },
  { ok: 'ok', time: '5 min ago', title: 'TechPio Autotask Staging', sub: 'Synchronization completed successfully', detail: '86 records processed' },
  { ok: 'warn', time: '18 min ago', title: 'TechPio ServiceNow', sub: 'Synchronization completed with warnings', detail: '3 records failed' },
  { ok: 'ok', time: '30 min ago', title: 'HaloPSA Production', sub: 'Synchronization completed successfully', detail: '210 records processed' },
  { ok: 'ok', time: '1 hr ago', title: 'BMS Staging', sub: 'Synchronization completed successfully', detail: '64 records processed' },
];

const statusBadge: Record<Health, string> = {
  Healthy: 'bg-green-100 text-green-700 dark:bg-green-950 dark:text-green-300',
  Warning: 'bg-amber-100 text-amber-700 dark:bg-amber-950 dark:text-amber-300',
  Critical: 'bg-red-100 text-red-700 dark:bg-red-950 dark:text-red-300',
};
const healthBar = (h: number) => (h >= 95 ? 'bg-green-500' : h >= 80 ? 'bg-amber-500' : 'bg-red-500');

function Avatar({ abbr, color, size = 'md' }: { abbr: string; color: string; size?: 'md' | 'lg' }) {
  const s = size === 'lg' ? 'h-12 w-12 text-sm' : 'h-8 w-8 text-[11px]';
  return <span className={`inline-flex items-center justify-center rounded-full font-bold ${s} ${color}`}>{abbr}</span>;
}

function BigStat({ icon: Icon, iconTone, value, label, sub, valueTone }: {
  icon: React.ElementType; iconTone: string; value: React.ReactNode; label: string; sub: string; valueTone?: string;
}) {
  return (
    <div className="flex items-start gap-3">
      <span className={`inline-flex h-10 w-10 items-center justify-center rounded-full ${iconTone}`}><Icon size={18} /></span>
      <div>
        <div className={`text-2xl font-semibold leading-tight ${valueTone ?? ''}`}>{value}</div>
        <div className="text-sm font-medium">{label}</div>
        <div className="text-xs text-[var(--muted)]">{sub}</div>
      </div>
    </div>
  );
}

export default function HealthPage() {
  const [now, setNow] = useState(0); // bump to simulate refresh
  return (
    <div className="space-y-5" key={now}>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Integration Health</h1>
          <p className="text-sm text-[var(--muted)]">Live status of each PSA connection.</p>
        </div>
        <div className="flex items-center gap-2">
          <button onClick={() => setNow((n) => n + 1)} className="inline-flex items-center gap-2 rounded-lg border border-[var(--border)] bg-[var(--surface)] px-3 py-2 text-sm font-medium hover:bg-[var(--bg)]">
            <RefreshCw size={15} /> Refresh
          </button>
          <button className="inline-flex items-center gap-2 rounded-lg border border-[var(--border)] bg-[var(--surface)] px-3 py-2 text-sm font-medium hover:bg-[var(--bg)]">
            <Filter size={15} /> All Connections <ChevronDown size={14} className="text-[var(--faint)]" />
          </button>
        </div>
      </div>

      {/* Featured connection */}
      <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-3">
            <Avatar abbr={FEATURED.abbr} color={FEATURED.color} size="lg" />
            <div>
              <div className="text-lg font-semibold">{FEATURED.name}</div>
              <div className="mt-0.5 flex items-center gap-2 text-sm text-[var(--muted)]">
                <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium ${statusBadge[FEATURED.status]}`}><CheckCircle2 size={11} /> {FEATURED.status}</span>
                Last synced: {FEATURED.lastSync}
              </div>
            </div>
          </div>
          <span className="inline-flex items-center gap-2 rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-1.5 text-sm">
            <Layers size={15} className="text-[var(--muted)]" /> <strong>{FEATURED.monitors}</strong> Monitors
          </span>
        </div>
        <div className="mt-5 grid grid-cols-2 gap-4 lg:grid-cols-4">
          <BigStat icon={Clock} iconTone="bg-blue-50 text-blue-600 dark:bg-blue-950/50 dark:text-blue-300" value={FEATURED.pending} label="Pending" sub="Requires attention" />
          <BigStat icon={Mail} iconTone="bg-amber-50 text-amber-600 dark:bg-amber-950/50 dark:text-amber-300" value={FEATURED.deadLetter} label="Dead-letter" sub="Email delivery issues" />
          <BigStat icon={AlertOctagon} iconTone="bg-red-50 text-red-600 dark:bg-red-950/50 dark:text-red-300" value={FEATURED.failed} label="Failed events" sub="Last 24 hours" />
          <BigStat icon={CheckCircle2} iconTone="bg-green-50 text-green-600 dark:bg-green-950/50 dark:text-green-300" value={`${FEATURED.overall}%`} valueTone="text-green-600 dark:text-green-400" label="Overall health" sub="All systems operational" />
        </div>
      </div>

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        {/* All connections */}
        <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] xl:col-span-2">
          <div className="flex items-center justify-between border-b border-[var(--border)] px-5 py-3.5">
            <h2 className="text-sm font-semibold">All PSA Connections</h2>
            <button className="inline-flex items-center gap-1.5 text-sm font-medium text-brand hover:underline"><Plus size={15} /> Add Connection</button>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="text-left text-[10px] uppercase tracking-wide text-[var(--faint)]">
                <tr className="border-b border-[var(--border)]">
                  <th className="px-5 py-2.5 font-medium">Connection</th>
                  <th className="px-2 py-2.5 font-medium">Status</th>
                  <th className="px-2 py-2.5 font-medium">Last Sync</th>
                  <th className="px-2 py-2.5 font-medium">Pending</th>
                  <th className="px-2 py-2.5 font-medium">Failed</th>
                  <th className="px-5 py-2.5 font-medium">Health</th>
                </tr>
              </thead>
              <tbody>
                {CONNS.map((c) => (
                  <tr key={c.name} className="border-b border-[var(--border)] last:border-0">
                    <td className="px-5 py-3"><span className="flex items-center gap-2.5"><Avatar abbr={c.abbr} color={c.color} /><span className="font-medium">{c.name}</span></span></td>
                    <td className="px-2 py-3"><span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${statusBadge[c.status]}`}>{c.status}</span></td>
                    <td className="px-2 py-3 text-[var(--muted)]">{c.lastSync}</td>
                    <td className="px-2 py-3"><span className={c.pending > 0 ? 'font-medium text-amber-600 dark:text-amber-400' : 'text-[var(--muted)]'}>{c.pending}</span></td>
                    <td className="px-2 py-3"><span className={c.failed > 0 ? 'font-medium text-red-600 dark:text-red-400' : 'text-[var(--muted)]'}>{c.failed}</span></td>
                    <td className="px-5 py-3">
                      <span className="flex items-center gap-2">
                        <span className="h-1.5 w-16 overflow-hidden rounded-full bg-[var(--bg)]"><span className={`block h-full rounded-full ${healthBar(c.health)}`} style={{ width: `${c.health}%` }} /></span>
                        <span className="tabular-nums text-xs text-[var(--muted)]">{c.health}%</span>
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="border-t border-[var(--border)] px-5 py-3">
            <button className="inline-flex items-center gap-1.5 text-sm font-medium text-brand hover:underline">View all connections <ArrowRight size={14} /></button>
          </div>
        </div>

        {/* Recent sync activity */}
        <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)]">
          <div className="flex items-center justify-between border-b border-[var(--border)] px-5 py-3.5">
            <h2 className="text-sm font-semibold">Recent Sync Activity</h2>
            <button className="text-sm font-medium text-brand hover:underline">View all</button>
          </div>
          <ul className="px-5 py-3">
            {ACTIVITY.map((a, i) => (
              <li key={i} className="flex gap-3 pb-4 last:pb-1">
                {a.ok === 'ok'
                  ? <CheckCircle2 size={17} className="mt-0.5 shrink-0 text-green-500" />
                  : <AlertTriangle size={17} className="mt-0.5 shrink-0 text-amber-500" />}
                <div className="min-w-0">
                  <div className="flex items-baseline justify-between gap-2">
                    <span className="truncate text-sm font-medium">{a.title}</span>
                    <span className="shrink-0 text-xs text-[var(--faint)]">{a.time}</span>
                  </div>
                  <div className="text-xs text-[var(--muted)]">{a.sub}</div>
                  <div className="text-xs text-[var(--faint)]">{a.detail}</div>
                </div>
              </li>
            ))}
          </ul>
        </div>
      </div>

      <div className="flex items-center gap-2 rounded-lg border border-blue-200 bg-blue-50 px-4 py-3 text-sm text-blue-800 dark:border-blue-900 dark:bg-blue-950/40 dark:text-blue-200">
        <span className="flex h-5 w-5 items-center justify-center rounded-full bg-blue-500 text-white text-[11px] font-bold">i</span>
        Integration health is calculated based on synchronization status, error rates, and response times.
        <a href="/user-guide.pdf" target="_blank" rel="noopener noreferrer" className="font-medium underline">Learn more</a>
      </div>
    </div>
  );
}
