'use client';

import { useQuery } from '@tanstack/react-query';
import { Activity } from 'lucide-react';
import { api } from '@/lib/api';

export default function HealthPage() {
  const { data, isError } = useQuery({ queryKey: ['health'], queryFn: api.health });

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-xl font-semibold">Integration Health</h1>
        <p className="text-sm text-[var(--muted)]">Live status of each PSA connection.</p>
      </div>

      {(isError || (data && data.length === 0)) && (
        <div className="flex flex-col items-center rounded-xl border border-dashed border-[var(--border)] px-6 py-12 text-center">
          <Activity className="mb-3 text-[var(--faint)]" size={26} />
          <p className="text-sm text-[var(--muted)]">No connections to monitor yet.</p>
        </div>
      )}

      {data && data.length > 0 && (
        <div className="grid gap-4 sm:grid-cols-2">
          {data.map((h) => (
            <div key={h.connectionId} className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5">
              <div className="flex items-center justify-between">
                <span className="font-medium">{h.name}</span>
                <span className="rounded-full bg-[var(--bg)] px-2.5 py-0.5 text-xs">{String(h.status)}</span>
              </div>
              <dl className="mt-4 grid grid-cols-3 gap-3 text-center">
                <Stat label="Pending" value={h.pendingJobs} />
                <Stat label="Dead-letter" value={h.deadLetterJobs} danger={h.deadLetterJobs > 0} />
                <Stat label="Failed events" value={h.failedSyncEvents} danger={h.failedSyncEvents > 0} />
              </dl>
              {h.lastError && <p className="mt-3 text-xs text-red-500">{h.lastError}</p>}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function Stat({ label, value, danger }: { label: string; value: number; danger?: boolean }) {
  return (
    <div>
      <div className={`text-2xl font-semibold tabular-nums ${danger ? 'text-red-500' : ''}`}>{value}</div>
      <div className="text-xs text-[var(--muted)]">{label}</div>
    </div>
  );
}
