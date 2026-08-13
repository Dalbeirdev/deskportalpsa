import { Ticket, MessageSquare, Paperclip, CircleDot, Timer } from 'lucide-react';
import { BrowserFrame, ProductScreen } from '@/components/marketing/ProductUI';

/**
 * Floating event cards, positioned so they overlap the frame's edges rather than sitting on the
 * screenshot — that overlap is what makes the composition read as depth instead of a sticker.
 * Hidden below lg, where there is no room for them and they would only crowd the product.
 */
const EVENTS = [
  { icon: Ticket, title: 'New ticket', body: '#10482 · VPN connection issue', pos: 'left-[-1.5rem] top-[12%]', delay: '0s' },
  { icon: MessageSquare, title: 'Technician reply', body: 'Michael · “Working on this now.”', pos: 'right-[-2rem] top-[30%]', delay: '-1.5s' },
  { icon: Paperclip, title: 'File attached', body: 'vpn-error-90-percent.png', pos: 'left-[-2.5rem] bottom-[24%]', delay: '-3s' },
  { icon: CircleDot, title: 'Status updated', body: 'New → In progress', pos: 'right-[-1rem] bottom-[10%]', delay: '-4.5s' },
  { icon: Timer, title: 'Time entry', body: '0.75 h · billable', pos: 'left-[8%] bottom-[-1.75rem]', delay: '-2.2s' },
];

export function HeroStage() {
  return (
    <div className="relative">
      <div className="dp-sheen relative overflow-hidden rounded-2xl">
        <BrowserFrame>
          <ProductScreen view="tickets" />
        </BrowserFrame>
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
