'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Megaphone, Plus, Pencil, Trash2, Check, X, Pin, Eye, EyeOff } from 'lucide-react';
import { api, type Announcement, type AnnouncementInput } from '@/lib/api';
import { CpHeader, AccessError } from '../_ui';

const empty: AnnouncementInput = { title: '', body: '', isPinned: false, isPublished: true };

export default function AnnouncementsPage() {
  const qc = useQueryClient();
  const { data, isLoading, error } = useQuery({ queryKey: ['cp-announcements'], queryFn: api.cpAnnouncements, retry: false });
  const [draft, setDraft] = useState<AnnouncementInput | null>(null);

  const save = useMutation({
    mutationFn: (input: AnnouncementInput) => api.cpSaveAnnouncement(input),
    onSuccess: () => { setDraft(null); qc.invalidateQueries({ queryKey: ['cp-announcements'] }); },
  });
  const del = useMutation({
    mutationFn: (id: string) => api.cpDeleteAnnouncement(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['cp-announcements'] }),
  });

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <CpHeader icon={Megaphone} title="Announcements" subtitle="Notices for the people in your organization who use the portal. Drafts stay hidden until you publish them." />

      {error ? <AccessError label="Announcements" /> : (
        <>
          <div className="flex justify-end">
            {!draft && <button onClick={() => setDraft({ ...empty })} className="inline-flex items-center gap-1.5 rounded-lg bg-brand px-3 py-2 text-sm font-medium text-brand-fg hover:opacity-90"><Plus size={15} /> New announcement</button>}
          </div>

          {draft && <Editor initial={draft} onCancel={() => setDraft(null)} onSave={(i) => save.mutate(i)} saving={save.isPending} />}

          {isLoading && <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-8 text-center text-sm text-[var(--muted)]">Loading…</div>}
          {data?.length === 0 && !draft && <div className="rounded-xl border border-dashed border-[var(--border)] p-8 text-center text-sm text-[var(--muted)]">No announcements yet.</div>}

          <div className="space-y-3">
            {data?.map((a) => <Card key={a.id} announcement={a} onSave={(i) => save.mutate(i)} onDelete={() => del.mutate(a.id)} saving={save.isPending} />)}
          </div>
        </>
      )}
    </div>
  );
}

function Card({ announcement, onSave, onDelete, saving }: { announcement: Announcement; onSave: (i: AnnouncementInput) => void; onDelete: () => void; saving: boolean }) {
  const [editing, setEditing] = useState(false);
  if (editing) return <Editor initial={announcement} onCancel={() => setEditing(false)} onSave={(i) => { onSave(i); setEditing(false); }} saving={saving} />;
  return (
    <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            {announcement.isPinned && <Pin size={14} className="shrink-0 text-brand" />}
            <h3 className="font-semibold">{announcement.title}</h3>
            {announcement.isPublished
              ? <span className="inline-flex items-center gap-1 rounded-full bg-green-100 px-2 py-0.5 text-[11px] font-medium text-green-700 dark:bg-green-950/60 dark:text-green-300"><Eye size={10} /> Published</span>
              : <span className="inline-flex items-center gap-1 rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-600 dark:bg-slate-800 dark:text-slate-300"><EyeOff size={10} /> Draft</span>}
          </div>
          {announcement.body && <p className="mt-1.5 whitespace-pre-wrap text-sm text-[var(--muted)]">{announcement.body}</p>}
          <p className="mt-2 text-xs text-[var(--faint)]">
            {announcement.authorName ? `${announcement.authorName} · ` : ''}
            {announcement.publishedAt ? new Date(announcement.publishedAt).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' }) : 'Not published'}
          </p>
        </div>
        <div className="flex shrink-0 items-center gap-1">
          <button onClick={() => setEditing(true)} aria-label="Edit" className="rounded-md p-1.5 text-[var(--muted)] hover:bg-[var(--bg)] hover:text-brand"><Pencil size={15} /></button>
          <button onClick={() => { if (window.confirm(`Delete "${announcement.title}"?`)) onDelete(); }} aria-label="Delete" className="rounded-md p-1.5 text-[var(--muted)] hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-950/50"><Trash2 size={15} /></button>
        </div>
      </div>
    </div>
  );
}

function Editor({ initial, onCancel, onSave, saving }: { initial: Announcement | AnnouncementInput; onCancel: () => void; onSave: (i: AnnouncementInput) => void; saving: boolean }) {
  const [f, setF] = useState<AnnouncementInput>({
    id: 'id' in initial ? initial.id : undefined,
    title: initial.title, body: initial.body ?? '', isPinned: initial.isPinned, isPublished: initial.isPublished,
  });
  return (
    <div className="rounded-xl border border-brand/40 bg-[var(--surface)] p-5">
      <input value={f.title} onChange={(e) => setF({ ...f, title: e.target.value })} placeholder="Announcement title"
        className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm font-medium outline-none focus:border-brand" />
      <textarea value={f.body ?? ''} onChange={(e) => setF({ ...f, body: e.target.value })} rows={4} placeholder="Write your announcement…"
        className="mt-3 w-full resize-y rounded-lg border border-[var(--border)] bg-[var(--bg)] p-3 text-sm outline-none focus:border-brand" />
      <div className="mt-3 flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-4 text-sm">
          <label className="flex items-center gap-2"><input type="checkbox" checked={f.isPinned} onChange={(e) => setF({ ...f, isPinned: e.target.checked })} className="h-4 w-4 accent-brand" /> Pin to top</label>
          <label className="flex items-center gap-2"><input type="checkbox" checked={f.isPublished} onChange={(e) => setF({ ...f, isPublished: e.target.checked })} className="h-4 w-4 accent-brand" /> Published</label>
        </div>
        <div className="flex items-center gap-2">
          <button onClick={onCancel} className="inline-flex items-center gap-1.5 rounded-lg border border-[var(--border)] px-3 py-1.5 text-sm text-[var(--muted)] hover:bg-[var(--bg)]"><X size={14} /> Cancel</button>
          <button onClick={() => f.title.trim() && onSave(f)} disabled={!f.title.trim() || saving} className="inline-flex items-center gap-1.5 rounded-lg bg-brand px-3 py-1.5 text-sm font-medium text-brand-fg hover:opacity-90 disabled:opacity-40"><Check size={14} /> Save</button>
        </div>
      </div>
    </div>
  );
}
