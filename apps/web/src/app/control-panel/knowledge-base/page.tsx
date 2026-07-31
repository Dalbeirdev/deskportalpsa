'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { BookOpen, Plus, Pencil, Trash2, Check, X, ChevronDown, Eye, EyeOff, Info } from 'lucide-react';
import { api, type FaqArticle, type FaqInput } from '@/lib/api';
import { CpHeader, AccessError, Field } from '../_ui';

const empty: FaqInput = { question: '', answer: '', category: '', isPublished: true, sortOrder: 0 };

export default function KnowledgeBasePage() {
  const qc = useQueryClient();
  const { data, isLoading, error } = useQuery({ queryKey: ['cp-faq'], queryFn: api.cpFaq, retry: false });
  const [draft, setDraft] = useState<FaqInput | null>(null);

  const save = useMutation({
    mutationFn: (input: FaqInput) => api.cpSaveFaq(input),
    onSuccess: () => { setDraft(null); qc.invalidateQueries({ queryKey: ['cp-faq'] }); },
  });
  const del = useMutation({
    mutationFn: (id: string) => api.cpDeleteFaq(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['cp-faq'] }),
  });

  // Group articles by category for display.
  const groups = useMemo(() => {
    const map = new Map<string, FaqArticle[]>();
    for (const a of data ?? []) {
      const key = a.category?.trim() || 'General';
      (map.get(key) ?? map.set(key, []).get(key)!).push(a);
    }
    return [...map.entries()].sort((a, b) => a[0].localeCompare(b[0]));
  }, [data]);

  return (
    <div className="mx-auto max-w-4xl space-y-5">
      <CpHeader icon={BookOpen} title="Knowledge Base / FAQ" subtitle="Answers to common questions for your users — grouped by category, published when you're ready." />
      <div className="flex items-start gap-2 rounded-lg border border-blue-200 bg-blue-50 px-4 py-3 text-sm text-blue-800 dark:border-blue-900 dark:bg-blue-950/40 dark:text-blue-200">
        <Info size={16} className="mt-0.5 shrink-0" /> <p>Draft articles stay hidden until you publish them. Use a <b>Category</b> to group related questions.</p>
      </div>

      {error ? <AccessError label="Knowledge Base" /> : (
        <>
          <div className="flex justify-end">
            <button onClick={() => setDraft({ ...empty })}
              className="inline-flex items-center gap-1.5 rounded-lg bg-brand px-3 py-2 text-sm font-medium text-brand-fg hover:opacity-90">
              <Plus size={15} /> Add article
            </button>
          </div>

          {draft && (
            <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)]">
              <Editor initial={draft} onCancel={() => setDraft(null)} onSave={(i) => save.mutate(i)} saving={save.isPending} />
            </div>
          )}

          {isLoading && <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-8 text-center text-sm text-[var(--muted)]">Loading…</div>}
          {!isLoading && groups.length === 0 && !draft && (
            <div className="rounded-xl border border-dashed border-[var(--border)] p-10 text-center text-sm text-[var(--muted)]">No articles yet — add your first FAQ.</div>
          )}

          {groups.map(([category, items]) => (
            <div key={category} className="rounded-xl border border-[var(--border)] bg-[var(--surface)]">
              <div className="flex items-center gap-2 border-b border-[var(--border)] px-5 py-3 text-xs font-semibold uppercase tracking-wide text-[var(--faint)]">
                {category} <span className="rounded-full bg-[var(--bg)] px-2 py-0.5 text-[10px] text-[var(--muted)]">{items.length}</span>
              </div>
              <div className="divide-y divide-[var(--border)]">
                {items.map((a) => <Article key={a.id} article={a} onSave={(i) => save.mutate(i)} onDelete={() => del.mutate(a.id)} saving={save.isPending} />)}
              </div>
            </div>
          ))}
        </>
      )}
    </div>
  );
}

function Article({ article, onSave, onDelete, saving }: { article: FaqArticle; onSave: (i: FaqInput) => void; onDelete: () => void; saving: boolean }) {
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState(false);
  if (editing) return <Editor initial={article} onCancel={() => setEditing(false)} onSave={(i) => { onSave(i); setEditing(false); }} saving={saving} />;
  return (
    <div className="px-5 py-3">
      <div className="flex items-center gap-3">
        <button onClick={() => setOpen((v) => !v)} className="flex min-w-0 flex-1 items-center gap-2 text-left">
          <ChevronDown size={16} className={`shrink-0 text-[var(--faint)] transition-transform ${open ? 'rotate-180' : ''}`} />
          <span className="truncate font-medium">{article.question}</span>
          {!article.isPublished && <span className="inline-flex shrink-0 items-center gap-1 rounded bg-[var(--bg)] px-1.5 py-0.5 text-[10px] font-medium text-[var(--faint)]"><EyeOff size={10} /> Draft</span>}
        </button>
        <button onClick={() => setEditing(true)} aria-label="Edit" className="rounded-md p-1.5 text-[var(--muted)] hover:bg-[var(--bg)] hover:text-brand"><Pencil size={15} /></button>
        <button onClick={() => { if (window.confirm('Delete this article?')) onDelete(); }} aria-label="Delete" className="rounded-md p-1.5 text-[var(--muted)] hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-950/50"><Trash2 size={15} /></button>
      </div>
      {open && <p className="mt-2 whitespace-pre-wrap pl-6 text-sm text-[var(--muted)]">{article.answer || '—'}</p>}
    </div>
  );
}

function Editor({ initial, onCancel, onSave, saving }: { initial: FaqArticle | FaqInput; onCancel: () => void; onSave: (i: FaqInput) => void; saving: boolean }) {
  const [f, setF] = useState<FaqInput>({
    id: 'id' in initial ? initial.id : undefined,
    question: initial.question, answer: initial.answer ?? '', category: initial.category ?? '',
    isPublished: initial.isPublished, sortOrder: initial.sortOrder,
  });
  return (
    <div className="bg-[var(--bg)]/40 px-5 py-4">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <Field label="Question" value={f.question} onChange={(v) => setF({ ...f, question: v })} placeholder="How do I reset my password?" className="sm:col-span-2" />
        <Field label="Category" value={f.category ?? ''} onChange={(v) => setF({ ...f, category: v })} placeholder="Access" />
      </div>
      <label className="mt-3 block text-xs font-medium text-[var(--muted)]">
        Answer
        <textarea value={f.answer ?? ''} onChange={(e) => setF({ ...f, answer: e.target.value })} rows={5}
          placeholder="Explain the answer clearly…"
          className="mt-1 w-full resize-y rounded-lg border border-[var(--border)] bg-[var(--bg)] p-3 text-sm outline-none focus:border-brand" />
      </label>
      <div className="mt-3 flex flex-wrap items-center justify-between gap-3">
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" checked={f.isPublished} onChange={(e) => setF({ ...f, isPublished: e.target.checked })} className="h-4 w-4 accent-brand" />
          {f.isPublished ? <><Eye size={14} className="text-green-600 dark:text-green-400" /> Published</> : <><EyeOff size={14} className="text-[var(--faint)]" /> Draft</>}
        </label>
        <div className="flex items-center gap-2">
          <button onClick={onCancel} className="inline-flex items-center gap-1.5 rounded-lg border border-[var(--border)] px-3 py-1.5 text-sm text-[var(--muted)] hover:bg-[var(--bg)]"><X size={14} /> Cancel</button>
          <button onClick={() => f.question.trim() && onSave(f)} disabled={!f.question.trim() || saving} className="inline-flex items-center gap-1.5 rounded-lg bg-brand px-3 py-1.5 text-sm font-medium text-brand-fg hover:opacity-90 disabled:opacity-40"><Check size={14} /> Save</button>
        </div>
      </div>
    </div>
  );
}
