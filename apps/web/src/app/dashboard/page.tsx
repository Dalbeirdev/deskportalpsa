'use client';

import Link from 'next/link';
import { RefreshCw, Plus, Calendar, ArrowUpRight, ArrowDownRight, TrendingUp, Clock, ShieldCheck, Plug } from 'lucide-react';
import { MiniSpark, TrendChart, Donut } from '@/components/charts';

// Representative overview data. Wire to /api/dashboard/* + /api/admin/health for live figures.
const STATS = [
  { label: 'Open Tickets', value: '124', delta: '+12 (10.7%)', up: true, spark: [90, 96, 104, 110, 98, 118, 124], color: '#3b82f6', tone: 'blue' },
  { label: 'Overdue Tickets', value: '18', delta: '-3 (14.3%)', up: false, spark: [24, 22, 25, 21, 20, 19, 18], color: '#ef4444', tone: 'red' },
  { label: 'SLA Compliance', value: '97.8%', delta: '+2.4% vs yesterday', up: true, spark: [95, 95.4, 96, 96.6, 97, 97.4, 97.8], color: '#22c55e', tone: 'green' },
  { label: 'Active Connections', value: '5', delta: 'All systems healthy', up: true, spark: [5, 5, 5, 5, 5, 5, 5], color: '#8b5cf6', tone: 'violet' },
];

const TREND = {
  labels: ['May 14', 'May 15', 'May 16', 'May 17', 'May 18', 'May 19', 'May 20'],
  created: [88, 97, 120, 110, 98, 127, 118],
  resolved: [60, 70, 82, 74, 68, 90, 82],
};

const RECENT = [
  { id: '2456', title: 'Printer not responding', who: 'Acme Corp · John Doe', priority: 'High', status: 'Open', when: '10 min ago' },
  { id: '2455', title: 'VPN connection issue', who: 'Global Solutions · Sarah Lee', priority: 'Medium', status: 'In Progress', when: '25 min ago' },
  { id: '2454', title: 'Microsoft 365 login problem', who: 'Tech Innovators · Mike Smith', priority: 'Low', status: 'Open', when: '1 hour ago' },
  { id: '2453', title: 'Email not syncing', who: 'Acme Corp · John Doe', priority: 'Low', status: 'Resolved', when: '2 hours ago' },
];

const HEALTH = [
  { name: 'ConnectWise', sync: '2 min ago', state: 'Healthy' },
  { name: 'Autotask', sync: '3 min ago', state: 'Healthy' },
  { name: 'HaloPSA', sync: '15 min ago', state: 'Delayed' },
  { name: 'ServiceNow', sync: '45 min ago', state: 'Offline' },
  { name: 'BMS', sync: '1 hour ago', state: 'Healthy' },
];

const TECHS = [
  { name: 'John Doe', closed: 42, sla: 98 },
  { name: 'Sarah Lee', closed: 39, sla: 96 },
  { name: 'Mike Smith', closed: 34, sla: 97 },
  { name: 'David Brown', closed: 28, sla: 95 },
];

const PRIORITY = [
  { label: 'Critical', value: 12, pct: '9.7%', color: '#ef4444' },
  { label: 'High', value: 34, pct: '27.4%', color: '#f97316' },
  { label: 'Medium', value: 58, pct: '46.8%', color: '#3b82f6' },
  { label: 'Low', value: 16, pct: '12.9%', color: '#94a3b8' },
  { label: 'Info', value: 4, pct: '3.2%', color: '#22c55e' },
];

const ACTIVITY = [
  { t: '10:35 AM', title: 'New ticket created', sub: '#2456 – Printer not responding', color: '#3b82f6' },
  { t: '10:22 AM', title: 'Ticket resolved', sub: '#2452 – Outlook not opening', color: '#22c55e' },
  { t: '10:15 AM', title: 'Autotask sync completed', sub: '12 tickets synchronized', color: '#8b5cf6' },
  { t: '09:58 AM', title: 'Technician assigned', sub: '#2455 – VPN connection issue', color: '#f59e0b' },
];

const toneBg: Record<string, string> = {
  blue: 'bg-blue-50 text-blue-600 dark:bg-blue-950/50 dark:text-blue-300',
  red: 'bg-red-50 text-red-600 dark:bg-red-950/50 dark:text-red-300',
  green: 'bg-green-50 text-green-600 dark:bg-green-950/50 dark:text-green-300',
  violet: 'bg-violet-50 text-violet-600 dark:bg-violet-950/50 dark:text-violet-300',
};
const statIcon: Record<string, typeof TrendingUp> = { blue: TrendingUp, red: Clock, green: ShieldCheck, violet: Plug };

function Badge({ text }: { text: string }) {
  const map: Record<string, string> = {
    Open: 'bg-blue-100 text-blue-700 dark:bg-blue-950 dark:text-blue-300',
    'In Progress': 'bg-amber-100 text-amber-700 dark:bg-amber-950 dark:text-amber-300',
    Resolved: 'bg-green-100 text-green-700 dark:bg-green-950 dark:text-green-300',
    Closed: 'bg-slate-200 text-slate-600 dark:bg-slate-800 dark:text-slate-300',
  };
  return <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${map[text] ?? map.Closed}`}>{text}</span>;
}

const prioDot: Record<string, string> = { Critical: '#ef4444', High: '#f97316', Medium: '#3b82f6', Low: '#94a3b8', Info: '#22c55e' };
const healthStyle: Record<string, { c: string; t: string }> = {
  Healthy: { c: '#22c55e', t: 'text-green-600 dark:text-green-400' },
  Delayed: { c: '#eab308', t: 'text-amber-600 dark:text-amber-400' },
  Offline: { c: '#ef4444', t: 'text-red-600 dark:text-red-400' },
};

function Card({ children, className = '' }: { children: React.ReactNode; className?: string }) {
  return <div className={`rounded-xl border border-[var(--border)] bg-[var(--surface)] ${className}`}>{children}</div>;
}
function CardHead({ title, action }: { title: string; action?: React.ReactNode }) {
  return (
    <div className="flex items-center justify-between border-b border-[var(--border)] px-5 py-3.5">
      <h2 className="text-sm font-semibold">{title}</h2>
      {action}
    </div>
  );
}
const ViewAll = ({ href = '#' }: { href?: string }) => (
  <Link href={href} className="text-xs font-medium text-brand hover:underline">View all</Link>
);

export default function Overview() {
  return (
    <div className="space-y-6">
      {/* Title + controls */}
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Overview</h1>
          <p className="text-sm text-[var(--muted)]">Real-time summary of your PSA operations and ticketing system</p>
        </div>
        <div className="flex items-center gap-2">
          <button className="inline-flex items-center gap-2 rounded-lg border border-[var(--border)] bg-[var(--surface)] px-3 py-2 text-sm hover:bg-[var(--bg)]">
            <RefreshCw size={15} /> Refresh
          </button>
          <button className="inline-flex items-center gap-2 rounded-lg border border-[var(--border)] bg-[var(--surface)] px-3 py-2 text-sm hover:bg-[var(--bg)]">
            <Calendar size={15} /> Last 24 hours
          </button>
          <Link href="/dashboard/tickets/new" className="inline-flex items-center gap-2 rounded-lg bg-brand px-3.5 py-2 text-sm font-medium text-brand-fg hover:opacity-90">
            <Plus size={16} /> New Ticket
          </Link>
        </div>
      </div>

      {/* Stat cards */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {STATS.map((s) => {
          const Icon = statIcon[s.tone];
          return (
            <Card key={s.label} className="p-5">
              <div className="flex items-start justify-between">
                <div className={`flex h-11 w-11 items-center justify-center rounded-xl ${toneBg[s.tone]}`}>
                  <Icon size={20} />
                </div>
                <MiniSpark points={s.spark} color={s.color} />
              </div>
              <div className="mt-3 text-sm text-[var(--muted)]">{s.label}</div>
              <div className="mt-0.5 text-3xl font-semibold tracking-tight tabular-nums">{s.value}</div>
              <div className={`mt-1 flex items-center gap-1 text-xs font-medium ${s.up ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'}`}>
                {s.up ? <ArrowUpRight size={13} /> : <ArrowDownRight size={13} />} {s.delta}
              </div>
            </Card>
          );
        })}
      </div>

      {/* Trend + Recent + Health */}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-12">
        <Card className="lg:col-span-5">
          <CardHead title="Ticket Trend" action={
            <span className="inline-flex items-center gap-3 text-xs text-[var(--muted)]">
              <span className="flex items-center gap-1.5"><i className="h-2 w-2 rounded-full" style={{ background: '#3b82f6' }} /> Created</span>
              <span className="flex items-center gap-1.5"><i className="h-2 w-2 rounded-full" style={{ background: '#22c55e' }} /> Resolved</span>
            </span>
          } />
          <div className="p-4"><TrendChart labels={TREND.labels} created={TREND.created} resolved={TREND.resolved} /></div>
        </Card>

        <Card className="lg:col-span-4">
          <CardHead title="Recent Tickets" action={<ViewAll href="/dashboard/tickets" />} />
          <ul className="divide-y divide-[var(--border)]">
            {RECENT.map((t) => (
              <li key={t.id} className="px-5 py-3">
                <div className="flex items-start justify-between gap-2">
                  <div>
                    <div className="text-sm"><span className="font-medium text-brand">#{t.id}</span> {t.title}</div>
                    <div className="mt-0.5 text-xs text-[var(--muted)]">{t.who}</div>
                  </div>
                  <span className="whitespace-nowrap text-xs text-[var(--faint)]">{t.when}</span>
                </div>
                <div className="mt-2 flex items-center gap-2">
                  <span className="flex items-center gap-1 text-xs text-[var(--muted)]">
                    <i className="h-1.5 w-1.5 rounded-full" style={{ background: prioDot[t.priority] }} /> {t.priority}
                  </span>
                  <span className="ml-auto"><Badge text={t.status} /></span>
                </div>
              </li>
            ))}
          </ul>
        </Card>

        <Card className="lg:col-span-3">
          <CardHead title="Integration Health" action={<ViewAll href="/dashboard/health" />} />
          <ul className="divide-y divide-[var(--border)]">
            {HEALTH.map((h) => (
              <li key={h.name} className="flex items-center gap-3 px-5 py-3">
                <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-[var(--bg)] text-xs font-semibold">{h.name[0]}</span>
                <div className="min-w-0">
                  <div className="text-sm font-medium">{h.name}</div>
                  <div className="text-xs text-[var(--muted)]">Last sync: {h.sync}</div>
                </div>
                <span className={`ml-auto flex items-center gap-1.5 text-xs font-medium ${healthStyle[h.state].t}`}>
                  <i className="h-2 w-2 rounded-full" style={{ background: healthStyle[h.state].c }} /> {h.state}
                </span>
              </li>
            ))}
          </ul>
        </Card>
      </div>

      {/* Bottom row */}
      <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-4">
        <Card>
          <CardHead title="Technician Performance" action={<ViewAll href="/dashboard/analytics" />} />
          <div className="px-5 py-2">
            {TECHS.map((t) => (
              <div key={t.name} className="flex items-center gap-3 py-2.5">
                <span className="flex h-7 w-7 items-center justify-center rounded-full bg-[var(--bg)] text-[10px] font-semibold">
                  {t.name.split(' ').map((n) => n[0]).join('')}
                </span>
                <span className="text-sm">{t.name}</span>
                <span className="ml-auto text-sm tabular-nums text-[var(--muted)]">{t.closed}</span>
                <div className="h-1.5 w-16 overflow-hidden rounded-full bg-[var(--bg)]">
                  <div className="h-full rounded-full bg-green-500" style={{ width: `${t.sla}%` }} />
                </div>
                <span className="w-9 text-right text-xs tabular-nums text-[var(--muted)]">{t.sla}%</span>
              </div>
            ))}
          </div>
        </Card>

        <Card>
          <CardHead title="Ticket Priority Distribution" />
          <div className="flex items-center gap-4 px-5 py-4">
            <Donut segments={PRIORITY} total={124} />
            <ul className="space-y-1.5 text-sm">
              {PRIORITY.map((p) => (
                <li key={p.label} className="flex items-center gap-2">
                  <i className="h-2.5 w-2.5 rounded-sm" style={{ background: p.color }} />
                  <span>{p.label}</span>
                  <span className="ml-auto tabular-nums text-[var(--muted)]">{p.value} ({p.pct})</span>
                </li>
              ))}
            </ul>
          </div>
        </Card>

        <Card>
          <CardHead title="Activity Timeline" action={<ViewAll href="/dashboard/audit" />} />
          <ul className="px-5 py-3">
            {ACTIVITY.map((a, i) => (
              <li key={i} className="flex gap-3 pb-4 last:pb-1">
                <div className="flex flex-col items-center">
                  <i className="mt-1 h-2.5 w-2.5 rounded-full" style={{ background: a.color }} />
                  {i < ACTIVITY.length - 1 && <span className="mt-1 w-px flex-1 bg-[var(--border)]" />}
                </div>
                <div>
                  <div className="text-xs text-[var(--faint)]">{a.t}</div>
                  <div className="text-sm font-medium">{a.title}</div>
                  <div className="text-xs text-[var(--muted)]">{a.sub}</div>
                </div>
              </li>
            ))}
          </ul>
        </Card>

        <Card>
          <CardHead title="SLA Compliance" action={<ViewAll href="/dashboard/analytics" />} />
          <div className="px-5 py-4">
            <div className="text-3xl font-semibold tabular-nums">97.8%</div>
            <div className="mt-1 flex items-center gap-1 text-xs font-medium text-green-600 dark:text-green-400">
              <ArrowUpRight size={13} /> 2.4% vs yesterday
            </div>
            <div className="mt-4 space-y-3">
              {[
                { label: 'Within SLA', v: 121, pct: 97.8, color: '#22c55e' },
                { label: 'At Risk', v: 2, pct: 1.6, color: '#eab308' },
                { label: 'Breached', v: 1, pct: 0.6, color: '#ef4444' },
              ].map((r) => (
                <div key={r.label}>
                  <div className="flex justify-between text-xs">
                    <span className="text-[var(--muted)]">{r.label}</span>
                    <span className="tabular-nums">{r.v} ({r.pct}%)</span>
                  </div>
                  <div className="mt-1 h-1.5 rounded-full bg-[var(--bg)]">
                    <div className="h-full rounded-full" style={{ width: `${Math.max(r.pct, 1)}%`, background: r.color }} />
                  </div>
                </div>
              ))}
            </div>
          </div>
        </Card>
      </div>
    </div>
  );
}
