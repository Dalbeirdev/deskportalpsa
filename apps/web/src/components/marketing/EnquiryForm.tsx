'use client';

import { useEffect, useMemo, useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { Check, Send, CalendarClock } from 'lucide-react';
import { api } from '@/lib/api';
import { CONTACT_EMAIL } from '@/components/marketing/MarketingFooter';
import { COUNTRIES, TIME_SLOTS, countryFor, countryFromTimeZone, canonicalTimeZone } from '@/lib/countries';

const field =
  'w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2.5 text-sm outline-none focus:border-brand';

function todayISO() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

/**
 * Contact and meeting requests — the same submission, with scheduling fields on the meeting form.
 *
 * Company and phone are mandatory for a meeting and optional on the contact form, because a demo
 * needs someone reachable while a question does not. The API enforces the same rule; `required`
 * here is a courtesy to the visitor, not the check that matters.
 *
 * The time zone follows the chosen country rather than being a field of its own. It is still shown
 * in full before sending, because an unstated zone is what books a meeting at the wrong hour — but
 * showing it is enough; making it a second control only invited the two to disagree.
 */
export function EnquiryForm({ kind, sourcePage }: { kind: 'contact' | 'meeting'; sourcePage: string }) {
  const meeting = kind === 'meeting';
  const [form, setForm] = useState({
    name: '', email: '', company: '', phone: '', message: '', website: '',
    country: 'GB', date: '', time: '10:00',
  });

  // Start from where the visitor actually is, then let them correct it.
  useEffect(() => {
    try {
      const detected = canonicalTimeZone(Intl.DateTimeFormat().resolvedOptions().timeZone);
      const guess = countryFromTimeZone(detected);
      if (guess) setForm((f) => ({ ...f, country: guess.code }));
    } catch {
      /* keep the defaults */
    }
  }, []);

  const set = (k: keyof typeof form) =>
    (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>) =>
      setForm((f) => ({ ...f, [k]: e.target.value }));

  const onCountry = (e: React.ChangeEvent<HTMLSelectElement>) => {
    setForm((f) => ({ ...f, country: e.target.value }));
  };

  const country = countryFor(form.country);
  const dial = country?.dial ?? '';
  const tz = country?.tz ?? 'Europe/London';

  /** Exactly what the request will carry, shown before it is sent. */
  const preferred = useMemo(() => {
    if (!meeting || !form.date) return '';
    const when = new Date(`${form.date}T${form.time}:00`);
    const pretty = Number.isNaN(when.getTime())
      ? `${form.date} ${form.time}`
      : when.toLocaleDateString(undefined, { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });
    return `${pretty} at ${form.time} (${tz})`;
  }, [meeting, form.date, form.time, tz]);

  const submit = useMutation({
    mutationFn: () =>
      api.submitEnquiry(kind, {
        name: form.name,
        email: form.email,
        company: form.company,
        phone: form.phone ? `${dial} ${form.phone}`.trim() : '',
        message: form.message,
        preferredTime: preferred,
        sourcePage,
        website: form.website,
      }),
  });

  if (submit.isSuccess) {
    return (
      <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-6">
        <p className="flex items-center gap-2 text-[15px] font-semibold">
          <Check size={18} className="text-brand" aria-hidden="true" />
          Thank you — we have it.
        </p>
        <p className="mt-2 text-sm leading-relaxed text-[var(--muted)]">
          {meeting
            ? `We will confirm by email for ${preferred || 'the time you suggested'}.`
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
          <input className={field} value={form.name} onChange={set('name')} required minLength={2} maxLength={120} autoComplete="name" />
        </label>
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium">Work email</span>
          <input className={field} type="email" value={form.email} onChange={set('email')} required maxLength={200} autoComplete="email" />
        </label>

        <label className="block">
          <span className="mb-1.5 block text-sm font-medium">
            Company {!meeting && <span className="font-normal text-[var(--faint)]">(optional)</span>}
          </span>
          <input className={field} value={form.company} onChange={set('company')} required={meeting} maxLength={160} autoComplete="organization" />
        </label>

        <label className="block">
          <span className="mb-1.5 block text-sm font-medium">Country</span>
          <select className={field} value={form.country} onChange={onCountry} autoComplete="country">
            {COUNTRIES.map((c) => (
              <option key={c.code} value={c.code}>{c.name} ({c.dial})</option>
            ))}
          </select>
        </label>

        <label className="block sm:col-span-2">
          <span className="mb-1.5 block text-sm font-medium">
            Phone {!meeting && <span className="font-normal text-[var(--faint)]">(optional)</span>}
          </span>
          <span className="flex">
            <span className="inline-flex shrink-0 items-center rounded-l-lg border border-r-0 border-[var(--border)] bg-[var(--surface)] px-3 text-sm text-[var(--muted)]">
              {dial}
            </span>
            <input
              className={`${field} rounded-l-none`}
              type="tel"
              inputMode="tel"
              value={form.phone}
              onChange={set('phone')}
              required={meeting}
              maxLength={40}
              autoComplete="tel-national"
              placeholder="7700 900123"
            />
          </span>
        </label>

        {meeting && (
          <>
            <label className="block">
              <span className="mb-1.5 block text-sm font-medium">Preferred date</span>
              <input className={field} type="date" value={form.date} onChange={set('date')} required min={todayISO()} />
            </label>
            <label className="block">
              <span className="mb-1.5 block text-sm font-medium">Preferred time</span>
              <select className={field} value={form.time} onChange={set('time')}>
                {TIME_SLOTS.map((t) => <option key={t} value={t}>{t}</option>)}
              </select>
            </label>
            <p className="text-xs text-[var(--muted)] sm:col-span-2">
              Times are in <span className="font-medium text-[var(--fg)]">{tz.replace(/_/g, ' ')}</span>,
              from the country you selected.
            </p>
          </>
        )}

        <label className="block sm:col-span-2">
          <span className="mb-1.5 block text-sm font-medium">
            {meeting ? 'What would you like to cover?' : 'How can we help?'}
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

      {meeting && preferred && (
        <p className="mt-4 flex items-start gap-2 rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2.5 text-[13px] text-[var(--muted)]">
          <CalendarClock size={15} className="mt-0.5 shrink-0 text-brand-mid" aria-hidden="true" />
          <span>
            We will aim for <span className="font-medium text-[var(--fg)]">{preferred}</span> and
            confirm by email.
          </span>
        </p>
      )}

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
          {submit.isPending ? 'Sending…' : meeting ? 'Request meeting' : 'Send message'}
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
