'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Inbox, Mail, Phone, Building2, CalendarClock, Check, RotateCcw } from 'lucide-react';
import { api, type Enquiry } from '@/lib/api';

type Status = 'New' | 'InProgress' | 'Closed';

// The API serialises enums as names, but a JSON-number serialiser setting would send ordinals —
// accept both rather than render "0" at someone.
const STATUS_NAMES: Status[] = ['New', 'InProgress', 'Closed'];
const KIND_NAMES = ['Contact', 'Meeting'];
const name = (v: string | number, names: string[]) =>
  typeof v === 'number' ? names[v] ?? String(v) : v;

const STATUS_STYLE: Record<Status, string> = {
  New: 'bg-brand/10 text-brand',
  InProgress: 'bg-amber-100 text-amber-700 dark:bg-amber-950 dark:text-amber-300',
  Closed: 'bg-slate-200 text-slate-600 dark:bg-slate-800 dark:text-slate-300',
};

function Row({ e }: { e: Enquiry }) {
  const qc = useQueryClient();
  const setStatus = useMutation({
    mutationFn: (s: Status) => api.setEnquiryStatus(e.id, s),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['enquiries'] }),
  });
  const status = name(e.status, STATUS_NAMES) as Status;
  const kind = name(e.kind, KIND_NAMES);

  return (
    <li className="px-5 py-4">
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm font-medium">{e.name}</span>
        <span className={`rounded px-1.5 py-0.5 text-[11px] font-medium ${STATUS_STYLE[status] ?? ''}`}>
          {status === 'InProgress' ? 'In progress' : status}
        </span>
        {kind === 'Meeting' && (
          <span className="inline-flex items-center gap-1 rounded bg-[var(--bg)] px-1.5 py-0.5 text-[11px] font-medium text-[var(--muted)]">
            <CalendarClock size={11} aria-hidden="true" /> Meeting request
          </span>
        )}
        <span className="flex-1" />
        <span className="text-xs text-[var(--muted)]">
          {new Date(e.createdAt).toLocaleString()}
        </span>
      </div>

      <div className="mt-1.5 flex flex-wrap gap-x-4 gap-y-1 text-xs text-[var(--muted)]">
        <a href={`mailto:${e.email}`} className="inline-flex items-center gap-1 hover:text-[var(--fg)]">
          <Mail size={12} aria-hidden="true" /> {e.email}
        </a>
        {e.company && (
          <span className="inline-flex items-center gap-1"><Building2 size={12} aria-hidden="true" /> {e.company}</span>
        )}
        {e.phone && (
          <span className="inline-flex items-center gap-1"><Phone size={12} aria-hidden="true" /> {e.phone}</span>
        )}
        {e.sourcePage && <span>from {e.sourcePage}</span>}
      </div>

      {e.preferredTime && (
        <p className="mt-2 text-xs text-[var(--muted)]">
          <span className="font-medium text-[var(--fg)]">Suggested times:</span> {e.preferredTime}
        </p>
      )}

      <p className="mt-2 whitespace-pre-wrap text-sm leading-relaxed">{e.message}</p>

      <div className="mt-3 flex gap-2">
        {status !== 'Closed' ? (
          <>
            {status === 'New' && (
              <button
                onClick={() => setStatus.mutate('InProgress')}
                className="rounded-lg border border-[var(--border)] px-2.5 py-1 text-xs hover:bg-[var(--bg)]"
              >
                Mark in progress
              </button>
            )}
            <button
              onClick={() => setStatus.mutate('Closed')}
              className="inline-flex items-center gap-1 rounded-lg border border-[var(--border)] px-2.5 py-1 text-xs hover:bg-[var(--bg)]"
            >
              <Check size={12} aria-hidden="true" /> Close
            </button>
          </>
        ) : (
          <button
            onClick={() => setStatus.mutate('New')}
            className="inline-flex items-center gap-1 rounded-lg border border-[var(--border)] px-2.5 py-1 text-xs hover:bg-[var(--bg)]"
          >
            <RotateCcw size={12} aria-hidden="true" /> Reopen
          </button>
        )}
      </div>
    </li>
  );
}

export default function EnquiriesPage() {
  const [filter, setFilter] = useState<Status | undefined>('New');
  const { data, isError } = useQuery({
    queryKey: ['enquiries', filter ?? 'all'],
    queryFn: () => api.enquiries(filter),
  });

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Enquiries</h1>
        <p className="mt-1 text-sm text-[var(--muted)]">
          Messages and meeting requests from the public site.
          {data ? ` ${data.newCount} unanswered of ${data.total}.` : ''}
        </p>
      </div>

      <div className="flex flex-wrap gap-1.5">
        {([undefined, 'New', 'InProgress', 'Closed'] as const).map((s) => (
          <button
            key={s ?? 'all'}
            onClick={() => setFilter(s)}
            className={`rounded-lg border px-3 py-1.5 text-xs font-medium ${
              filter === s
                ? 'border-brand bg-brand/10 text-brand'
                : 'border-[var(--border)] text-[var(--muted)] hover:bg-[var(--bg)]'
            }`}
          >
            {s === undefined ? 'All' : s === 'InProgress' ? 'In progress' : s}
          </button>
        ))}
      </div>

      <div className="overflow-hidden rounded-xl border border-[var(--border)] bg-[var(--surface)]">
        {isError ? (
          <p className="px-5 py-10 text-center text-sm text-[var(--muted)]">Could not load enquiries.</p>
        ) : !data || data.items.length === 0 ? (
          <p className="flex flex-col items-center gap-2 px-5 py-14 text-center text-sm text-[var(--muted)]">
            <Inbox size={22} aria-hidden="true" />
            Nothing here yet. Messages sent from the contact and booking pages arrive in this list.
          </p>
        ) : (
          <ul className="divide-y divide-[var(--border)]">
            {data.items.map((e) => <Row key={e.id} e={e} />)}
          </ul>
        )}
      </div>
    </div>
  );
}
