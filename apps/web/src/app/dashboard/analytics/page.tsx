'use client';

import { useState } from 'react';
import {
  Calendar, ChevronDown, Gauge, ClipboardList, CheckCircle2, FolderOpen, Clock, Sparkles,
  TrendingUp, TrendingDown, RefreshCw, Users, Star, Info, Download,
} from 'lucide-react';
import { MiniSpark, TrendChart, BarChart, LineChart, Donut } from '@/components/charts';

const DAY = ['May 14', 'May 15', 'May 16', 'May 17', 'May 18', 'May 19', 'May 20'];
const RANGES = ['Today', 'Yesterday', '7 Days', '30 Days', '90 Days', 'Custom'];

const KPIS = [
  { label: 'Assigned Tickets', value: '1,284', delta: '8.2%', up: true, spark: [180, 170, 185, 178, 172, 190, 184], color: '#3b82f6', icon: ClipboardList, tone: 'blue' },
  { label: 'Resolved Tickets', value: '1,182', delta: '10.1%', up: true, spark: [150, 160, 170, 168, 172, 178, 182], color: '#22c55e', icon: CheckCircle2, tone: 'green' },
  { label: 'Open Tickets', value: '320', delta: '5.4%', up: false, spark: [360, 350, 345, 338, 330, 325, 320], color: '#f97316', icon: FolderOpen, tone: 'orange' },
  { label: 'Overdue Tickets', value: '42', delta: '12.5%', up: false, spark: [56, 52, 50, 48, 46, 44, 42], color: '#ef4444', icon: Clock, tone: 'red' },
];

const INSIGHTS = [
  { icon: CheckCircle2, color: '#22c55e', title: 'SLA compliance improved', sub: '3.2% compared to last 7 days', up: true },
  { icon: TrendingDown, color: '#22c55e', title: 'Ticket volume decreased', sub: '5.4% in open tickets', up: true },
  { icon: Users, color: '#f59e0b', title: '2 technicians have high workload', sub: 'Review workload distribution' },
  { icon: RefreshCw, color: '#22c55e', title: 'Autotask sync successful', sub: 'Last sync 3 minutes ago' },
  { icon: Star, color: '#3b82f6', title: 'CSAT score is excellent', sub: '4.8/5 based on 624 reviews' },
];

const TREND = { created: [110, 130, 165, 150, 140, 205, 185], resolved: [45, 60, 100, 85, 70, 108, 95] };
const RESOLUTION = [3.7, 3.9, 4.1, 3.8, 3.4, 3.2, 3.1];
const SLA = [95, 96, 97.5, 96.5, 97, 98.5, 98.2];

const ACTIVITIES = [
  { t: '10:24 AM', icon: Users, color: '#3b82f6', title: 'Ticket #2456 assigned', sub: 'John Doe' },
  { t: '10:18 AM', icon: CheckCircle2, color: '#22c55e', title: 'Ticket #2455 resolved', sub: 'Sarah Lee' },
  { t: '10:15 AM', icon: RefreshCw, color: '#8b5cf6', title: 'Autotask sync completed', sub: '12 tickets synchronized' },
  { t: '10:10 AM', icon: Clock, color: '#f59e0b', title: 'SLA breach alert', sub: 'Ticket #2448' },
  { t: '10:05 AM', icon: Users, color: '#3b82f6', title: 'New customer added', sub: 'Acme Corporation' },
];

const TECHS = [
  { name: 'John Doe', resolved: 128, time: '2h 45m', sla: 98 },
  { name: 'Sarah Lee', resolved: 112, time: '3h 15m', sla: 96 },
  { name: 'Mike Smith', resolved: 98, time: '3h 42m', sla: 94 },
  { name: 'David Brown', resolved: 84, time: '4h 05m', sla: 92 },
  { name: 'Emily Davis', resolved: 76, time: '4h 25m', sla: 90 },
];

const DIST = [
  { label: 'Critical', value: 42, pct: '3.3%', color: '#ef4444' },
  { label: 'High', value: 286, pct: '22.3%', color: '#f97316' },
  { label: 'Medium', value: 612, pct: '47.7%', color: '#3b82f6' },
  { label: 'Low', value: 258, pct: '20.1%', color: '#22c55e' },
  { label: 'Info', value: 86, pct: '6.6%', color: '#94a3b8' },
];

const WORKLOAD = [
  { name: 'Sarah Lee', pct: 92, color: '#ef4444' },
  { name: 'John Doe', pct: 68, color: '#f59e0b' },
  { name: 'Mike Smith', pct: 64, color: '#f59e0b' },
  { name: 'David Brown', pct: 51, color: '#22c55e' },
  { name: 'Emily Davis', pct: 38, color: '#22c55e' },
];

const CSAT = [
  { star: 5, count: 468, pct: 75 },
  { star: 4, count: 124, pct: 20 },
  { star: 3, count: 24, pct: 4 },
  { star: 2, count: 6, pct: 1 },
  { star: 1, count: 2, pct: 0 },
];

const tone: Record<string, string> = {
  blue: 'bg-blue-50 text-blue-600 dark:bg-blue-950/50 dark:text-blue-300',
  green: 'bg-green-50 text-green-600 dark:bg-green-950/50 dark:text-green-300',
  orange: 'bg-orange-50 text-orange-600 dark:bg-orange-950/50 dark:text-orange-300',
  red: 'bg-red-50 text-red-600 dark:bg-red-950/50 dark:text-red-300',
};

function Card({ children, className = '' }: { children: React.ReactNode; className?: string }) {
  return <div className={`rounded-xl border border-[var(--border)] bg-[var(--surface)] ${className}`}>{children}</div>;
}
function Head({ title, right, hint }: { title: string; right?: React.ReactNode; hint?: boolean }) {
  return (
    <div className="flex items-center justify-between border-b border-[var(--border)] px-5 py-3.5">
      <h2 className="flex items-center gap-1.5 text-sm font-semibold">{title}{hint && <Info size={13} className="text-[var(--faint)]" />}</h2>
      {right}
    </div>
  );
}
const RangeChip = ({ label }: { label: string }) => (
  <span className="inline-flex items-center gap-1 rounded-lg border border-[var(--border)] bg-[var(--surface)] px-2.5 py-1 text-xs text-[var(--muted)]">
    {label} <ChevronDown size={12} />
  </span>
);

export default function Analytics() {
  const [range, setRange] = useState('7 Days');
  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Productivity Analytics</h1>
        <p className="text-sm text-[var(--muted)]">Technician and team performance across connected systems.</p>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap items-center gap-2">
        <button className="inline-flex items-center gap-2 rounded-lg border border-[var(--border)] bg-[var(--surface)] px-3 py-2 text-sm">
          <Calendar size={15} /> May 14 – May 20, 2025 <ChevronDown size={14} className="text-[var(--faint)]" />
        </button>
        <div className="inline-flex rounded-lg border border-[var(--border)] bg-[var(--surface)] p-0.5">
          {RANGES.map((r) => (
            <button key={r} onClick={() => setRange(r)}
              className={`rounded-md px-2.5 py-1.5 text-xs font-medium ${range === r ? 'bg-brand text-brand-fg' : 'text-[var(--muted)] hover:text-[var(--fg)]'}`}>
              {r}
            </button>
          ))}
        </div>
        <div className="ml-auto flex items-center gap-2">
          <RangeChip label="Technician: All" /><RangeChip label="Company: All" /><RangeChip label="PSA: All" />
        </div>
      </div>

      {/* Score + KPIs + Insights */}
      <div className="grid grid-cols-1 gap-4 xl:grid-cols-4">
        <div className="xl:col-span-3">
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 xl:grid-cols-5">
            <Card className="p-4">
              <div className="flex items-center gap-2 text-sm text-[var(--muted)]"><Gauge size={16} className="text-brand" /> Productivity Score</div>
              <div className="mt-1 text-3xl font-semibold text-brand">94%</div>
              <div className="mt-1 flex items-center gap-1 text-xs font-medium text-green-600 dark:text-green-400"><TrendingUp size={12} /> 7.6% vs 7 days</div>
              <div className="mt-3 h-1.5 rounded-full bg-[var(--bg)]"><div className="h-full rounded-full bg-brand" style={{ width: '94%' }} /></div>
              <div className="mt-2 text-xs text-[var(--muted)]">Excellent · updated 2 min ago</div>
            </Card>
            {KPIS.map((k) => {
              const Icon = k.icon;
              return (
                <Card key={k.label} className="p-4">
                  <div className={`inline-flex h-9 w-9 items-center justify-center rounded-lg ${tone[k.tone]}`}><Icon size={17} /></div>
                  <div className="mt-2 text-sm text-[var(--muted)]">{k.label}</div>
                  <div className="text-2xl font-semibold tabular-nums">{k.value}</div>
                  <div className={`flex items-center gap-1 text-xs font-medium ${k.up ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'}`}>
                    {k.up ? <TrendingUp size={12} /> : <TrendingDown size={12} />} {k.delta} <span className="text-[var(--faint)]">vs 7 days</span>
                  </div>
                  <div className="mt-2"><MiniSpark points={k.spark} color={k.color} width={140} height={30} /></div>
                </Card>
              );
            })}
          </div>
          <p className="mt-2 text-[11px] text-[var(--faint)]">
            Productivity scores are operational indicators only and must not be used as the sole basis for employee performance decisions.
          </p>
        </div>

        <Card className="p-4 xl:col-span-1">
          <div className="mb-3 flex items-center gap-2 text-sm font-semibold"><Sparkles size={16} className="text-brand" /> Insights</div>
          <ul className="space-y-3">
            {INSIGHTS.map((it, i) => {
              const Icon = it.icon;
              return (
                <li key={i} className="flex gap-2.5">
                  <span className="mt-0.5"><Icon size={16} style={{ color: it.color }} /></span>
                  <div>
                    <div className="text-sm font-medium leading-tight">{it.title}</div>
                    <div className="text-xs text-[var(--muted)]">{it.sub}</div>
                  </div>
                </li>
              );
            })}
          </ul>
        </Card>
      </div>

      {/* Charts + Recent activities */}
      <div className="grid grid-cols-1 gap-4 xl:grid-cols-4">
        <div className="grid grid-cols-1 gap-4 md:grid-cols-3 xl:col-span-3">
          <Card>
            <Head title="Created vs Resolved Tickets" right={<RangeChip label="Last 7 days" />} />
            <div className="px-4 pb-2 pt-3">
              <div className="mb-1 flex gap-4 text-xs text-[var(--muted)]">
                <span className="flex items-center gap-1.5"><i className="h-2 w-2 rounded-full bg-[#3b82f6]" /> Created</span>
                <span className="flex items-center gap-1.5"><i className="h-2 w-2 rounded-full bg-[#22c55e]" /> Resolved</span>
              </div>
              <TrendChart labels={DAY} created={TREND.created} resolved={TREND.resolved} height={200} />
            </div>
          </Card>
          <Card>
            <Head title="Average Resolution Time" hint right={<RangeChip label="Last 7 days" />} />
            <div className="px-4 pb-2 pt-3">
              <div className="text-2xl font-semibold">3h 18m</div>
              <div className="mb-2 flex items-center gap-2 text-xs text-[var(--muted)]">
                Target: 4h <span className="inline-flex items-center gap-1 rounded bg-green-100 px-1.5 py-0.5 font-medium text-green-700 dark:bg-green-950 dark:text-green-300"><TrendingDown size={11} /> 0.7h better</span>
              </div>
              <BarChart values={RESOLUTION} labels={DAY} color="#3b82f6" height={170} unit="h" />
            </div>
          </Card>
          <Card>
            <Head title="SLA Compliance Trend" hint right={<RangeChip label="Last 7 days" />} />
            <div className="px-4 pb-2 pt-3">
              <div className="text-2xl font-semibold">98.2%</div>
              <div className="mb-2 flex items-center gap-1 text-xs font-medium text-green-600 dark:text-green-400"><TrendingUp size={12} /> 3.2% vs 7 days</div>
              <LineChart labels={DAY} values={SLA} color="#22c55e" height={170} yMin={80} yMax={100} unit="%" />
            </div>
          </Card>
        </div>

        <Card className="xl:col-span-1">
          <Head title="Recent Activities" right={<a href="/dashboard/audit" className="text-xs font-medium text-brand hover:underline">View all</a>} />
          <ul className="px-5 py-3">
            {ACTIVITIES.map((a, i) => {
              const Icon = a.icon;
              return (
                <li key={i} className="flex gap-3 pb-4 last:pb-1">
                  <span className="mt-0.5 text-xs tabular-nums text-[var(--faint)]" style={{ minWidth: 56 }}>{a.t}</span>
                  <Icon size={15} style={{ color: a.color }} className="mt-0.5 shrink-0" />
                  <div>
                    <div className="text-sm font-medium leading-tight">{a.title}</div>
                    <div className="text-xs text-[var(--muted)]">{a.sub}</div>
                  </div>
                </li>
              );
            })}
          </ul>
        </Card>
      </div>

      {/* Bottom row */}
      <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-4">
        <Card>
          <Head title="Technician Performance" right={<a href="#" className="text-xs font-medium text-brand hover:underline">View all</a>} />
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="text-left text-[10px] uppercase tracking-wide text-[var(--faint)]">
                <tr className="border-b border-[var(--border)]"><th className="px-4 py-2 font-medium">Technician</th><th className="px-2 py-2 font-medium">Resolved</th><th className="px-2 py-2 font-medium">Time</th><th className="px-4 py-2 font-medium">SLA</th></tr>
              </thead>
              <tbody>
                {TECHS.map((t) => (
                  <tr key={t.name} className="border-b border-[var(--border)] last:border-0">
                    <td className="px-4 py-2.5">
                      <span className="flex items-center gap-2"><span className="flex h-6 w-6 items-center justify-center rounded-full bg-[var(--bg)] text-[9px] font-semibold">{t.name.split(' ').map((n) => n[0]).join('')}</span>{t.name}</span>
                    </td>
                    <td className="px-2 py-2.5 tabular-nums">{t.resolved}</td>
                    <td className="px-2 py-2.5 tabular-nums text-[var(--muted)]">{t.time}</td>
                    <td className="px-4 py-2.5">
                      <span className="flex items-center gap-1.5"><span className="tabular-nums">{t.sla}%</span><span className="h-1.5 w-10 overflow-hidden rounded-full bg-[var(--bg)]"><span className="block h-full rounded-full bg-green-500" style={{ width: `${t.sla}%` }} /></span></span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Card>

        <Card>
          <Head title="Ticket Distribution" />
          <div className="flex items-center gap-3 px-5 py-4">
            <Donut segments={DIST} total={1284} size={150} />
            <ul className="space-y-1.5 text-xs">
              {DIST.map((d) => (
                <li key={d.label} className="flex items-center gap-2"><i className="h-2.5 w-2.5 rounded-sm" style={{ background: d.color }} /><span>{d.label}</span><span className="ml-auto tabular-nums text-[var(--muted)]">{d.value} ({d.pct})</span></li>
              ))}
            </ul>
          </div>
        </Card>

        <Card>
          <Head title="Technician Workload" right={<a href="#" className="text-xs font-medium text-brand hover:underline">View all</a>} />
          <div className="space-y-3 px-5 py-4">
            {WORKLOAD.map((w) => (
              <div key={w.name}>
                <div className="mb-1 flex items-center justify-between text-sm">
                  <span className="flex items-center gap-2"><span className="flex h-6 w-6 items-center justify-center rounded-full bg-[var(--bg)] text-[9px] font-semibold">{w.name.split(' ').map((n) => n[0]).join('')}</span>{w.name}</span>
                  <span className="tabular-nums text-[var(--muted)]">{w.pct}%</span>
                </div>
                <div className="h-1.5 rounded-full bg-[var(--bg)]"><div className="h-full rounded-full" style={{ width: `${w.pct}%`, background: w.color }} /></div>
              </div>
            ))}
          </div>
        </Card>

        <Card>
          <Head title="Customer Satisfaction" right={<a href="#" className="text-xs font-medium text-brand hover:underline">View report</a>} />
          <div className="px-5 py-4">
            <div className="flex items-end gap-2">
              <span className="text-3xl font-semibold">4.8</span><span className="pb-1 text-sm text-[var(--muted)]">/ 5</span>
            </div>
            <div className="mt-1 flex gap-0.5 text-amber-400">{[0, 1, 2, 3, 4].map((i) => <Star key={i} size={16} fill="currentColor" />)}</div>
            <div className="mt-1 text-xs text-[var(--muted)]">Based on 624 reviews</div>
            <div className="mt-3 space-y-1.5">
              {CSAT.map((c) => (
                <div key={c.star} className="flex items-center gap-2 text-xs">
                  <span className="flex w-8 items-center gap-0.5 tabular-nums">{c.star}<Star size={10} className="text-amber-400" fill="currentColor" /></span>
                  <span className="h-1.5 flex-1 overflow-hidden rounded-full bg-[var(--bg)]"><span className="block h-full rounded-full bg-green-500" style={{ width: `${c.pct}%` }} /></span>
                  <span className="w-16 text-right tabular-nums text-[var(--muted)]">{c.count} ({c.pct}%)</span>
                </div>
              ))}
            </div>
          </div>
        </Card>
      </div>
    </div>
  );
}
