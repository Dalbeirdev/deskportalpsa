'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Palette, Save, CheckCircle2, AlertTriangle, Rocket } from 'lucide-react';
import { api } from '@/lib/api';
import { CpHeader, AccessError, Field } from '../_ui';

const HEX = /^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$/;

export default function BrandingPage() {
  const qc = useQueryClient();
  const { data, isLoading, error } = useQuery({ queryKey: ['cp-branding'], queryFn: api.cpBranding, retry: false });
  const [displayName, setDisplayName] = useState('');
  const [logoUrl, setLogoUrl] = useState('');
  const [accent, setAccent] = useState('');
  const [dirty, setDirty] = useState(false);

  useEffect(() => {
    if (data && !dirty) { setDisplayName(data.displayName ?? ''); setLogoUrl(data.logoUrl ?? ''); setAccent(data.accentColor ?? ''); }
  }, [data, dirty]);

  const save = useMutation({
    mutationFn: () => api.cpSaveBranding({ displayName: displayName || null, logoUrl: logoUrl || null, accentColor: accent || null }),
    onSuccess: () => { setDirty(false); qc.invalidateQueries({ queryKey: ['cp-branding'] }); },
  });

  const accentValid = !accent || HEX.test(accent);
  const previewAccent = accentValid && accent ? accent : '#2563eb';
  const set = (fn: (v: string) => void) => (v: string) => { fn(v); setDirty(true); };

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <CpHeader icon={Palette} title="Branding" subtitle="Personalize how your portal looks — a display name, a logo, and an accent color." />

      {error ? <AccessError label="Branding" /> : isLoading ? (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-8 text-center text-sm text-[var(--muted)]">Loading…</div>
      ) : (
        <div className="grid gap-5 md:grid-cols-2">
          <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5">
            <div className="space-y-3">
              <Field label="Display name" value={displayName} onChange={set(setDisplayName)} placeholder="Acme Dental Portal" />
              <Field label="Logo URL" value={logoUrl} onChange={set(setLogoUrl)} placeholder="https://…/logo.png" />
              <label className="block text-xs font-medium text-[var(--muted)]">
                Accent color
                <div className="mt-1 flex items-center gap-2">
                  <input type="color" value={previewAccent} onChange={(e) => set(setAccent)(e.target.value)} className="h-9 w-12 shrink-0 cursor-pointer rounded border border-[var(--border)] bg-[var(--bg)]" />
                  <input value={accent} onChange={(e) => set(setAccent)(e.target.value)} placeholder="#2563eb"
                    className={`w-full rounded-lg border bg-[var(--bg)] px-3 py-2 text-sm outline-none ${accentValid ? 'border-[var(--border)] focus:border-brand' : 'border-red-400'}`} />
                </div>
                {!accentValid && <span className="mt-1 block text-[11px] text-red-500">Enter a hex color like #2563eb.</span>}
              </label>
            </div>
            <div className="mt-4 flex items-center justify-between gap-3">
              <div className="text-xs">
                {save.isError && <span className="inline-flex items-center gap-1 text-red-600 dark:text-red-400"><AlertTriangle size={13} /> {(save.error as Error)?.message ?? 'Save failed'}</span>}
                {save.isSuccess && !dirty && <span className="inline-flex items-center gap-1 text-green-600 dark:text-green-400"><CheckCircle2 size={13} /> Saved</span>}
                {dirty && !save.isPending && <span className="text-[var(--faint)]">Unsaved changes</span>}
              </div>
              <button onClick={() => save.mutate()} disabled={!dirty || !accentValid || save.isPending}
                className="inline-flex items-center gap-2 rounded-lg bg-brand px-4 py-2 text-sm font-medium text-brand-fg hover:opacity-90 disabled:opacity-40">
                <Save size={15} /> {save.isPending ? 'Saving…' : 'Save'}
              </button>
            </div>
          </div>

          {/* Live preview */}
          <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5">
            <div className="mb-3 text-xs font-semibold uppercase tracking-wide text-[var(--faint)]">Preview</div>
            <div className="overflow-hidden rounded-xl border border-[var(--border)]">
              <div className="flex items-center gap-2.5 px-4 py-3" style={{ backgroundColor: previewAccent }}>
                {logoUrl
                  ? <img src={logoUrl} alt="logo" className="h-7 w-7 rounded object-contain" onError={(e) => { (e.currentTarget.style.display = 'none'); }} />
                  : <span className="flex h-7 w-7 items-center justify-center rounded bg-white/20 text-white"><Rocket size={15} /></span>}
                <span className="font-semibold text-white">{displayName || 'Your Portal'}</span>
              </div>
              <div className="space-y-2 p-4">
                <div className="h-2.5 w-3/4 rounded bg-[var(--bg)]" />
                <div className="h-2.5 w-1/2 rounded bg-[var(--bg)]" />
                <button className="mt-2 rounded-lg px-3 py-1.5 text-xs font-medium text-white" style={{ backgroundColor: previewAccent }}>Primary action</button>
              </div>
            </div>
            <p className="mt-3 text-xs text-[var(--faint)]">The accent color is applied to headers and primary buttons in your portal.</p>
          </div>
        </div>
      )}
    </div>
  );
}
