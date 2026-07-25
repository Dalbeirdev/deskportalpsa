'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { RotateCw, ListChecks } from 'lucide-react';
import { api } from '@/lib/api';

// BackgroundJobStatus enum: 0 Queued, 1 Running, 2 Succeeded, 3 Failed, 4 DeadLettered
const STATUS_LABEL: Record<number, string> = { 0: 'Queued', 1: 'Running', 2: 'Succeeded', 3: 'Failed', 4: 'Dead-lettered' };

export default function JobsPage() {
  const qc = useQueryClient();
  const { data, isError } = useQuery({ queryKey: ['jobs'], queryFn: () => api.jobs() });

  const reprocess = useMutation({
    mutationFn: (id: string) => api.reprocessJob(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['jobs'] }),
  });

  const isDeadLettered = (s: string | number) => String(s) === '4' || String(s) === 'DeadLettered';

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-xl font-semibold">Background Jobs</h1>
        <p className="text-sm text-[var(--muted)]">Monitor sync jobs and reprocess anything that dead-lettered.</p>
      </div>

      {(isError || (data && data.length === 0)) && (
        <div className="flex flex-col items-center rounded-xl border border-dashed border-[var(--border)] px-6 py-12 text-center">
          <ListChecks className="mb-3 text-[var(--faint)]" size={26} />
          <p className="text-sm text-[var(--muted)]">No jobs in the queue.</p>
        </div>
      )}

      {data && data.length > 0 && (
        <div className="overflow-x-auto rounded-xl border border-[var(--border)] bg-[var(--surface)]">
          <table className="w-full text-sm">
            <thead className="text-left text-xs uppercase tracking-wide text-[var(--muted)]">
              <tr className="border-b border-[var(--border)]">
                <th className="px-4 py-3 font-medium">Type</th>
                <th className="px-4 py-3 font-medium">Status</th>
                <th className="px-4 py-3 font-medium">Attempts</th>
                <th className="px-4 py-3 font-medium">Last error</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody>
              {data.map((j) => (
                <tr key={j.id} className="border-b border-[var(--border)] last:border-0">
                  <td className="px-4 py-3 font-mono text-xs">{j.jobType}</td>
                  <td className="px-4 py-3">{STATUS_LABEL[Number(j.status)] ?? String(j.status)}</td>
                  <td className="px-4 py-3 tabular-nums">{j.attempts}/{j.maxAttempts}</td>
                  <td className="px-4 py-3 max-w-xs truncate text-[var(--muted)]">{j.lastError ?? '—'}</td>
                  <td className="px-4 py-3 text-right">
                    {isDeadLettered(j.status) && (
                      <button
                        onClick={() => reprocess.mutate(j.id)}
                        disabled={reprocess.isPending}
                        className="inline-flex items-center gap-1.5 rounded-lg border border-[var(--border)] px-2.5 py-1.5 text-xs font-medium hover:bg-[var(--bg)] disabled:opacity-50"
                      >
                        <RotateCw size={13} /> Reprocess
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
