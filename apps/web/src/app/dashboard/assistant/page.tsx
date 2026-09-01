'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Sparkles, CheckCircle2, AlertTriangle, ShieldAlert } from 'lucide-react';
import { api } from '@/lib/api';

/**
 * Assistant settings. Off until an administrator turns it on, because using it sends ticket text to
 * Google — a decision an MSP holding other companies' data has to make deliberately, not inherit.
 */
export default function AssistantSettingsPage() {
  const qc = useQueryClient();
  const { data, isLoading, error } = useQuery({ queryKey: ['assistant-settings'], queryFn: api.assistantSettings, retry: false });

  const [enabled, setEnabled] = useState(false);
  const [includeInternal, setIncludeInternal] = useState(false);
  const [model, setModel] = useState('gemini-2.0-flash');
  const [apiKey, setApiKey] = useState('');

  useEffect(() => {
    if (!data) return;
    setEnabled(data.isEnabled);
    setIncludeInternal(data.includeInternalNotes);
    setModel(data.model);
  }, [data]);

  const save = useMutation({
    mutationFn: () => api.saveAssistantSettings({
      isEnabled: enabled,
      includeInternalNotes: includeInternal,
      model,
      // Blank means "keep the stored key" — the form is never given the existing value back.
      apiKey: apiKey.trim() || undefined,
    }),
    onSuccess: () => {
      setApiKey('');
      qc.invalidateQueries({ queryKey: ['assistant-settings'] });
      qc.invalidateQueries({ queryKey: ['assistant-availability'] });
    },
  });

  if (isLoading) return <p className="text-sm text-[var(--muted)]">Loading…</p>;
  if (error) {
    return (
      <div className="rounded-xl border border-dashed border-[var(--border)] p-8 text-center text-sm text-[var(--muted)]">
        You do not have permission to manage assistant settings.
      </div>
    );
  }

  return (
    <div className="max-w-2xl space-y-5">
      <header>
        <h1 className="flex items-center gap-2 text-xl font-semibold">
          <Sparkles size={19} className="text-indigo-600 dark:text-indigo-300" /> Assistant
        </h1>
        <p className="mt-1 text-sm text-[var(--muted)]">
          Adds a panel to each ticket that can summarise the thread, draft or improve a reply, suggest
          next steps and explain PSA errors. It never sends anything to a client or writes to your PSA.
        </p>
      </header>

      {/* The consequence, before the switch — not buried in help text after it. */}
      <div className="flex gap-3 rounded-xl border border-amber-200 bg-amber-50 p-4 dark:border-amber-900/60 dark:bg-amber-950/30">
        <ShieldAlert size={17} className="mt-0.5 shrink-0 text-amber-600 dark:text-amber-400" />
        <div className="text-sm text-amber-900 dark:text-amber-200">
          <p className="font-medium">Ticket text is sent to Google.</p>
          <p className="mt-1 text-[13px] leading-relaxed">
            When a technician uses the assistant, that ticket&apos;s title, description and public
            conversation are sent to the Gemini API under your own key. You hold your clients&apos;
            data — confirm your agreements permit this before switching it on.
          </p>
        </div>
      </div>

      <div className="space-y-4 rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5">
        <label className="flex items-start gap-3">
          <input type="checkbox" checked={enabled} onChange={(e) => setEnabled(e.target.checked)} className="mt-1" />
          <span>
            <span className="block text-sm font-medium">Enable the assistant</span>
            <span className="block text-xs text-[var(--muted)]">Shows the panel on every ticket for staff. Clients never see it.</span>
          </span>
        </label>

        <label className="block">
          <span className="mb-1 block text-xs font-medium">
            Google API key {data?.hasKey && <span className="font-normal text-green-600 dark:text-green-400">· a key is stored</span>}
          </span>
          <input
            type="password"
            value={apiKey}
            onChange={(e) => setApiKey(e.target.value)}
            placeholder={data?.hasKey ? 'Stored — type a new key only to replace it' : 'Paste your Gemini API key'}
            autoComplete="off"
            className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm outline-none focus:border-brand"
          />
          <span className="mt-1 block text-xs text-[var(--muted)]">
            Create one at Google AI Studio. Stored encrypted, like your PSA credentials, and never shown again.
          </span>
        </label>

        <label className="block">
          <span className="mb-1 block text-xs font-medium">Model</span>
          <input
            value={model}
            onChange={(e) => setModel(e.target.value)}
            className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm outline-none focus:border-brand"
          />
          <span className="mt-1 block text-xs text-[var(--muted)]">
            gemini-2.0-flash is fast and inexpensive. Change it without a deploy if you move models.
          </span>
        </label>

        <label className="flex items-start gap-3">
          <input type="checkbox" checked={includeInternal} onChange={(e) => setIncludeInternal(e.target.checked)} className="mt-1" />
          <span>
            <span className="block text-sm font-medium">Also send internal notes and time entries</span>
            <span className="block text-xs text-[var(--muted)]">
              Off by default. These carry rates and private commentary, and the useful answers come from
              the public thread anyway.
            </span>
          </span>
        </label>

        <div className="flex flex-wrap items-center gap-3 border-t border-[var(--border)] pt-4">
          <button
            onClick={() => save.mutate()}
            disabled={save.isPending}
            className="rounded-lg bg-brand px-4 py-2 text-sm font-medium text-brand-fg hover:opacity-90 disabled:opacity-50"
          >
            {save.isPending ? 'Saving…' : 'Save settings'}
          </button>
          {save.isSuccess && (
            <span className="inline-flex items-center gap-1.5 text-xs text-green-600 dark:text-green-400">
              <CheckCircle2 size={14} /> Saved.
            </span>
          )}
          {save.isError && (
            <span className="inline-flex items-center gap-1.5 text-xs text-red-600 dark:text-red-400">
              <AlertTriangle size={14} /> {(save.error as Error)?.message ?? 'Save failed.'}
            </span>
          )}
        </div>
      </div>
    </div>
  );
}
