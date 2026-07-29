'use client';

import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';

export default function ProfilePage() {
  const { data, isError } = useQuery({ queryKey: ['profile'], queryFn: api.profile });

  const rows = [
    { label: 'Name', value: data?.displayName ?? '—' },
    { label: 'Email', value: data?.email ?? '—' },
    { label: 'Role', value: data?.isCompanyAdministrator ? 'Company administrator' : 'Client user' },
  ];

  return (
    <div className="mx-auto max-w-xl space-y-5">
      <div>
        <h1 className="text-xl font-semibold">Profile</h1>
        <p className="text-sm text-[var(--muted)]">Your account details.</p>
      </div>

      <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-6">
        {isError && (
          <p className="mb-4 text-sm text-[var(--muted)]">Sign in to load your profile. This preview runs without a live backend.</p>
        )}
        <dl className="divide-y divide-[var(--border)]">
          {rows.map((r) => (
            <div key={r.label} className="flex justify-between py-3 text-sm">
              <dt className="text-[var(--muted)]">{r.label}</dt>
              <dd className="font-medium">{r.value}</dd>
            </div>
          ))}
        </dl>
      </div>
    </div>
  );
}
