'use client';

import { useState } from 'react';
import { Ticket, MessageSquare, Paperclip, History, Settings2, BarChart3, Building2 } from 'lucide-react';
import { BrowserFrame, ProductScreen, type ProductView } from '@/components/marketing/ProductUI';

const TABS: { id: ProductView; label: string; icon: typeof Ticket; url: string; note: string }[] = [
  { id: 'tickets', label: 'Service desk', icon: Ticket, url: 'portal.yourmsp.com/tickets', note: 'Everything open, with the ticket your technician is on already selected.' },
  { id: 'client', label: 'Client portal', icon: Building2, url: 'portal.yourmsp.com/acme', note: 'What your client sees: their own requests, and one button to raise another.' },
  { id: 'conversation', label: 'Conversation', icon: MessageSquare, url: 'portal.yourmsp.com/tickets/10482', note: 'One thread for client and technician. Internal notes never cross over.' },
  { id: 'files', label: 'Files', icon: Paperclip, url: 'portal.yourmsp.com/tickets/10482/files', note: 'Screenshots from the client and documents from the PSA, in one list.' },
  { id: 'timeline', label: 'Timeline', icon: History, url: 'portal.yourmsp.com/tickets/10482/history', note: 'Every movement, and which system it came from.' },
  { id: 'admin', label: 'Connections', icon: Settings2, url: 'portal.yourmsp.com/admin/connections', note: 'Your PSA connections, their health, and how fields map.' },
  { id: 'reporting', label: 'Reporting', icon: BarChart3, url: 'portal.yourmsp.com/analytics', note: 'Hours, resolution and SLA, drawn from the same data your PSA holds.' },
];

/**
 * Lets a visitor walk the product rather than read about it.
 *
 * The frame keeps a fixed minimum height so switching tabs cannot make the page jump — a layout
 * shift here would undo the impression the section exists to create.
 */
export function ProductTabs() {
  const [active, setActive] = useState<ProductView>('tickets');
  const current = TABS.find((t) => t.id === active)!;

  return (
    <div>
      <div className="mb-5 flex snap-x gap-2 overflow-x-auto pb-1" role="tablist" aria-label="Product views">
        {TABS.map(({ id, label, icon: Icon }) => {
          const on = id === active;
          return (
            <button
              key={id}
              role="tab"
              aria-selected={on}
              onClick={() => setActive(id)}
              className={`inline-flex shrink-0 snap-start items-center gap-2 rounded-full border px-3.5 py-2 text-[13px] font-medium transition-colors ${
                on
                  ? 'border-brand bg-brand text-brand-fg'
                  : 'border-[var(--border)] bg-[var(--surface)] text-[var(--muted)] hover:text-[var(--fg)]'
              }`}
            >
              <Icon size={14} aria-hidden="true" />
              {label}
            </button>
          );
        })}
      </div>

      <BrowserFrame url={current.url}>
        <div key={active} className="dp-rise min-h-[19rem]">
          <ProductScreen view={active} />
        </div>
      </BrowserFrame>

      <p className="mt-3 text-[13px] text-[var(--muted)]">{current.note}</p>
    </div>
  );
}
