'use client';

import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { Check, Send } from 'lucide-react';
import { api } from '@/lib/api';
import { CONTACT_EMAIL } from '@/components/marketing/MarketingFooter';

const field =
  'w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2.5 text-sm outline-none focus:border-brand';

/**
 * Contact and meeting requests, which are the same submission with one extra field. Enquiries are
 * stored in the portal rather than emailed — this deployment has no mail transport, and a form that
 * pretends to send is a lead quietly thrown away.
 */
export function EnquiryForm({ kind, sourcePage }: { kind: 'contact' | 'meeting'; sourcePage: string }) {
  const [form, setForm] = useState({
    name: '', email: '', company: '', phone: '', message: '', preferredTime: '', website: '',
  });
  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) =>
    setForm((f) => ({ ...f, [k]: e.target.value }));

  const submit = useMutation({
    mutationFn: () => api.submitEnquiry(kind, { ...form, sourcePage }),
  });

  if (submit.isSuccess) {
    return (
      <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-6">
        <p className="flex items-center gap-2 text-[15px] font-semibold">
          <Check size={18} className="text-brand" aria-hidden="true" />
          Thank you — we have it.
        </p>
        <p className="mt-2 text-sm leading-relaxed text-[var(--muted)]">
          {kind === 'meeting'
            ? 'We will confirm a time by email, using the slots you suggested.'
            : 'We reply by email, usually within one business day.'}{' '}
          If it is urgent, write to{' '}
          <a className="text-brand underline underline-offset-2" href={`mailto:${CONTACT_EMAIL}`}>
            {CONTACT_EMAIL}
          </a>.
        </p>
      </div>
    );
  }

  return (
    <form
      onSubmit={(e) => { e.preventDefault(); submit.mutate(); }}
      className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5 sm:p-6"
    >
      <div className="grid gap-4 sm:grid-cols-2">
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium">Your name</span>
          <input className={field} value={form.name} onChange={set('name')} required minLength={2} maxLength={120} />
        </label>
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium">Work email</span>
          <input className={field} type="email" value={form.email} onChange={set('email')} required maxLength={200} />
        </label>
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium">
            Company <span className="font-normal text-[var(--faint)]">(optional)</span>
          </span>
          <input className={field} value={form.company} onChange={set('company')} maxLength={160} />
        </label>
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium">
            Phone <span className="font-normal text-[var(--faint)]">(optional)</span>
          </span>
          <input className={field} value={form.phone} onChange={set('phone')} maxLength={60} />
        </label>

        {kind === 'meeting' && (
          <label className="block sm:col-span-2">
            <span className="mb-1.5 block text-sm font-medium">When suits you?</span>
            <input
              className={field}
              value={form.preferredTime}
              onChange={set('preferredTime')}
              maxLength={200}
              placeholder="e.g. Tuesday or Wednesday afternoon, UK time"
            />
            <span className="mt-1.5 block text-xs text-[var(--muted)]">
              In your own words, with your time zone — a picker that guesses the zone books the wrong hour.
            </span>
          </label>
        )}

        <label className="block sm:col-span-2">
          <span className="mb-1.5 block text-sm font-medium">
            {kind === 'meeting' ? 'What would you like to cover?' : 'How can we help?'}
          </span>
          <textarea
            className={`${field} min-h-[130px] resize-y`}
            value={form.message}
            onChange={set('message')}
            required
            minLength={10}
            maxLength={4000}
          />
        </label>
      </div>

      {/* Honeypot: hidden from people, irresistible to bots. Not display:none — some bots skip that. */}
      <div aria-hidden="true" className="absolute left-[-9999px] h-0 w-0 overflow-hidden">
        <label>
          Website
          <input tabIndex={-1} autoComplete="off" value={form.website} onChange={set('website')} />
        </label>
      </div>

      <div className="mt-5 flex flex-wrap items-center gap-3">
        <button
          type="submit"
          disabled={submit.isPending}
          className="inline-flex items-center gap-2 rounded-lg bg-brand px-4 py-2.5 text-sm font-medium text-brand-fg hover:opacity-90 disabled:opacity-50"
        >
          <Send size={15} aria-hidden="true" />
          {submit.isPending ? 'Sending…' : kind === 'meeting' ? 'Request meeting' : 'Send message'}
        </button>
        <span className="text-xs text-[var(--muted)]">
          We use your details only to reply. No newsletter, no third parties.
        </span>
      </div>

      {submit.isError && (
        <p role="alert" className="mt-3 text-sm text-red-600 dark:text-red-400">
          {submit.error instanceof Error ? submit.error.message : 'Something went wrong.'}{' '}
          You can also email <a className="underline" href={`mailto:${CONTACT_EMAIL}`}>{CONTACT_EMAIL}</a>.
        </p>
      )}
    </form>
  );
}
