'use client';

import { AlertTriangle } from 'lucide-react';

/** Standard control-panel page heading. */
export function CpHeader({ icon: Icon, title, subtitle }: { icon: React.ElementType; title: string; subtitle: string }) {
  return (
    <div>
      <h1 className="flex items-center gap-2 text-2xl font-semibold tracking-tight">
        <Icon size={22} className="text-brand" /> {title}
      </h1>
      <p className="mt-1 text-sm text-[var(--muted)]">{subtitle}</p>
    </div>
  );
}

/** Shown when the caller lacks access to a section (API returned 403) or the API is unreachable. */
export function AccessError({ label }: { label: string }) {
  return (
    <div className="flex items-center gap-2 rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-200">
      <AlertTriangle size={16} /> You don&apos;t have access to {label}, or the portal isn&apos;t reachable.
    </div>
  );
}

/** A labelled text input used across the CP-2 editors. */
export function Field({ label, value, onChange, placeholder, type = 'text', className = '' }: {
  label: string; value: string; onChange: (v: string) => void; placeholder?: string; type?: string; className?: string;
}) {
  return (
    <label className={`block text-xs font-medium text-[var(--muted)] ${className}`}>
      {label}
      <input
        type={type} value={value} onChange={(e) => onChange(e.target.value)} placeholder={placeholder}
        className="mt-1 w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm text-[var(--fg)] outline-none focus:border-brand"
      />
    </label>
  );
}
