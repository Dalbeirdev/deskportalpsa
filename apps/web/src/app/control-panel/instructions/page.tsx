'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { FileText, Globe, Building2, Save, CheckCircle2, AlertTriangle, Info, Clock } from 'lucide-react';
import { api, type Instruction } from '@/lib/api';

export default function InstructionsPage() {
  const { data, isLoading, error } = useQuery({ queryKey: ['cp-instructions'], queryFn: api.cpInstructions, retry: false });

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div>
        <h1 className="flex items-center gap-2 text-2xl font-semibold tracking-tight">
          <FileText size={22} className="text-brand" /> Ticket Service Instructions
        </h1>
        <p className="mt-1 text-sm text-[var(--muted)]">
          When a ticket is assigned to the technical team, these are the instructions our technicians follow for your account.
        </p>
      </div>

      <div className="flex items-start gap-2 rounded-lg border border-blue-200 bg-blue-50 px-4 py-3 text-sm text-blue-800 dark:border-blue-900 dark:bg-blue-950/40 dark:text-blue-200">
        <Info size={16} className="mt-0.5 shrink-0" />
        <p>
          <strong>Company-wide instructions</strong> apply to every ticket. <strong>Account instructions</strong> add
          detail for a specific account and appear alongside the company-wide ones on the ticket.
        </p>
      </div>

      {isLoading && <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-8 text-center text-sm text-[var(--muted)]">Loading instructions…</div>}
      {error && (
        <div className="flex items-center gap-2 rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-200">
          <AlertTriangle size={16} /> You don&apos;t have access to Ticket Instructions, or the portal isn&apos;t reachable.
        </div>
      )}

      {data && (
        <div className="space-y-5">
          <InstructionCard
            icon={Globe}
            title="Company-wide (Global)"
            subtitle="Followed for every ticket, across all accounts"
            instruction={data.global}
          />
          {data.accounts.map((acc) => (
            <InstructionCard
              key={acc.clientCompanyId ?? 'account'}
              icon={Building2}
              title={acc.accountName}
              subtitle="Account-specific instructions"
              instruction={acc}
            />
          ))}
        </div>
      )}
    </div>
  );
}

function InstructionCard({ icon: Icon, title, subtitle, instruction }: {
  icon: React.ElementType; title: string; subtitle: string; instruction: Instruction;
}) {
  const qc = useQueryClient();
  const [body, setBody] = useState(instruction.body);
  const [dirty, setDirty] = useState(false);

  // Keep the textarea in sync if the query refetches and the user hasn't started editing.
  useEffect(() => { if (!dirty) setBody(instruction.body); }, [instruction.body, dirty]);

  const save = useMutation({
    mutationFn: () => api.cpSaveInstruction(instruction.clientCompanyId, body),
    onSuccess: () => { setDirty(false); qc.invalidateQueries({ queryKey: ['cp-instructions'] }); },
  });

  return (
    <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)]">
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--border)] px-5 py-3.5">
        <div className="flex items-center gap-2.5">
          <span className="inline-flex h-9 w-9 items-center justify-center rounded-lg bg-brand/10 text-brand"><Icon size={17} /></span>
          <div>
            <div className="text-sm font-semibold">{title}</div>
            <div className="text-xs text-[var(--muted)]">{subtitle}</div>
          </div>
        </div>
        {instruction.lastEditedBy && instruction.updatedAt && (
          <span className="inline-flex items-center gap-1.5 text-xs text-[var(--faint)]">
            <Clock size={12} /> Edited by {instruction.lastEditedBy}
          </span>
        )}
      </div>
      <div className="p-5">
        <textarea
          value={body}
          onChange={(e) => { setBody(e.target.value); setDirty(true); }}
          rows={7}
          placeholder="e.g. Escalate all new-user setup tickets. Verify email platform (O365 vs other) before changes. Check with the end user if the issue is critical."
          className="w-full resize-y rounded-lg border border-[var(--border)] bg-[var(--bg)] p-3 font-mono text-sm leading-relaxed outline-none focus:border-brand"
        />
        <div className="mt-3 flex items-center justify-between gap-3">
          <div className="text-xs">
            {save.isError && <span className="inline-flex items-center gap-1 text-red-600 dark:text-red-400"><AlertTriangle size={13} /> {(save.error as Error)?.message ?? 'Save failed'}</span>}
            {save.isSuccess && !dirty && <span className="inline-flex items-center gap-1 text-green-600 dark:text-green-400"><CheckCircle2 size={13} /> Saved</span>}
            {dirty && !save.isPending && <span className="text-[var(--faint)]">Unsaved changes</span>}
          </div>
          <button
            onClick={() => save.mutate()}
            disabled={!dirty || save.isPending}
            className="inline-flex items-center gap-2 rounded-lg bg-brand px-4 py-2 text-sm font-medium text-brand-fg transition-opacity hover:opacity-90 disabled:opacity-40"
          >
            <Save size={15} /> {save.isPending ? 'Saving…' : 'Save'}
          </button>
        </div>
      </div>
    </div>
  );
}
