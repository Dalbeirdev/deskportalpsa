const stats = [
  { label: 'Open tickets', value: '—' },
  { label: 'Overdue', value: '—' },
  { label: 'SLA compliance', value: '—' },
  { label: 'Active connections', value: '—' },
];

export default function DashboardOverview() {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-semibold">Overview</h1>
        <p className="text-sm text-[var(--muted)]">
          Foundation shell. Live metrics arrive with the technician &amp; manager dashboard phase.
        </p>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {stats.map((s) => (
          <div
            key={s.label}
            className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5"
          >
            <div className="text-sm text-[var(--muted)]">{s.label}</div>
            <div className="mt-2 text-2xl font-semibold">{s.value}</div>
          </div>
        ))}
      </div>

      <div className="rounded-xl border border-dashed border-[var(--border)] p-8 text-center text-sm text-[var(--muted)]">
        Connect a PSA to begin. PSA connection management ships in the Administration phase.
      </div>
    </div>
  );
}
