'use client';

import { useQuery } from '@tanstack/react-query';
import { ShieldCheck } from 'lucide-react';
import { api } from '@/lib/api';

export default function AuditPage() {
  const { data, isError } = useQuery({ queryKey: ['audit'], queryFn: api.audit });

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-xl font-semibold">Audit Log</h1>
        <p className="text-sm text-[var(--muted)]">Immutable record of administrative and security events.</p>
      </div>

      {(isError || (data && data.length === 0)) && (
        <div className="flex flex-col items-center rounded-xl border border-dashed border-[var(--border)] px-6 py-12 text-center">
          <ShieldCheck className="mb-3 text-[var(--faint)]" size={26} />
          <p className="text-sm text-[var(--muted)]">No audit entries yet.</p>
        </div>
      )}

      {data && data.length > 0 && (
        <div className="overflow-x-auto rounded-xl border border-[var(--border)] bg-[var(--surface)]">
          <table className="w-full text-sm">
            <thead className="text-left text-xs uppercase tracking-wide text-[var(--muted)]">
              <tr className="border-b border-[var(--border)]">
                <th className="px-4 py-3 font-medium">When</th>
                <th className="px-4 py-3 font-medium">Actor</th>
                <th className="px-4 py-3 font-medium">Action</th>
                <th className="px-4 py-3 font-medium">Entity</th>
              </tr>
            </thead>
            <tbody>
              {data.map((a) => (
                <tr key={a.id} className="border-b border-[var(--border)] last:border-0">
                  <td className="px-4 py-3 text-[var(--muted)]">{new Date(a.createdAt).toLocaleString()}</td>
                  <td className="px-4 py-3">{a.actorDisplayName ?? '—'}</td>
                  <td className="px-4 py-3 font-mono text-xs">{a.action}</td>
                  <td className="px-4 py-3 text-[var(--muted)]">{a.entityType}{a.entityId ? ` #${a.entityId.slice(0, 8)}` : ''}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
