import { Ticket, MessageSquare, Paperclip, CircleDot, Bell, ArrowDown } from 'lucide-react';
import { BrowserFrame, ProductScreen } from '@/components/marketing/ProductUI';
import { PsaRotator } from '@/components/marketing/PsaRotator';

/**
 * Floating event cards, positioned so they overlap the frame's edges rather than sitting on the
 * screenshot — that overlap is what makes the composition read as depth instead of a sticker.
 * Hidden below lg, where there is no room for them and they would only crowd the product.
 */
/** Categories, never records — nothing here names a person, a company or a ticket. */
const EVENTS = [
  { icon: Ticket, title: 'Support request', body: 'Submitted by a client', pos: 'left-[-1.5rem] top-[14%]', delay: '0s' },
  { icon: MessageSquare, title: 'Response received', body: 'From your support team', pos: 'right-[-2rem] top-[30%]', delay: '-1.5s' },
  { icon: Paperclip, title: 'File shared', body: 'Screenshot attached', pos: 'left-[-2.5rem] bottom-[30%]', delay: '-3s' },
  { icon: CircleDot, title: 'Status updated', body: 'Open → In progress', pos: 'right-[-1rem] bottom-[16%]', delay: '-4.5s' },
  { icon: Bell, title: 'Client notified', body: 'No email chain needed', pos: 'left-[10%] bottom-[-1.5rem]', delay: '-2.2s' },
];

export function HeroStage() {
  return (
    <div className="relative">
      <div className="dp-sheen relative overflow-hidden rounded-2xl">
        <BrowserFrame>
          <ProductScreen view="requests" />
        </BrowserFrame>
      </div>

      {/* The PSA layer, named but interchangeable — the whole positioning in one strip. */}
      <div className="mt-5 flex flex-col items-center gap-2">
        <ArrowDown size={16} className="text-brand-mid" aria-hidden="true" />
        <div className="flex flex-wrap items-center justify-center gap-x-3 gap-y-1 rounded-2xl border border-[var(--border)] bg-[var(--surface)]/85 px-4 py-3 backdrop-blur">
          <span className="text-[11.5px] font-medium uppercase tracking-widest text-[var(--faint)]">
            Connects to
          </span>
          <PsaRotator />
        </div>
      </div>

      <div aria-hidden="true" className="pointer-events-none hidden lg:block">
        {EVENTS.map(({ icon: Icon, title, body, pos, delay }) => (
          <div
            key={title}
            className={`dp-float absolute ${pos} w-max max-w-[15rem] rounded-xl border border-[var(--border)] bg-[var(--surface)]/95 px-3 py-2.5 shadow-[0_12px_32px_-12px_rgba(11,18,32,0.35)] backdrop-blur`}
            style={{ animationDelay: delay }}
          >
            <p className="flex items-center gap-1.5 text-[12px] font-semibold">
              <Icon size={13} className="text-brand-mid" /> {title}
            </p>
            <p className="mt-0.5 text-[11px] text-[var(--muted)]">{body}</p>
          </div>
        ))}
      </div>
    </div>
  );
}
