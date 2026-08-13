'use client';

import { useEffect, useState } from 'react';
import { PSA_PLATFORMS, STATUS_LABEL } from '@/lib/psaPlatforms';

/**
 * Cycles the PSA name under the hero so the multi-PSA claim is made by the page itself rather
 * than by a sentence about it.
 *
 * Holds still for anyone who asked for reduced motion — a name swapping under its own steam is
 * exactly the kind of movement that setting exists to stop. The badge always reports the real
 * status of whichever platform is showing, so the rotation can never imply a connector exists.
 */
export function PsaRotator({ interval = 2600 }: { interval?: number }) {
  const [i, setI] = useState(0);

  useEffect(() => {
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;
    const id = window.setInterval(() => setI((n) => (n + 1) % PSA_PLATFORMS.length), interval);
    return () => window.clearInterval(id);
  }, [interval]);

  const psa = PSA_PLATFORMS[i];
  const live = psa.status === 'available';

  return (
    <span className="inline-flex min-h-[2rem] items-center gap-2">
      <span key={psa.id} className="dp-rise text-[15px] font-semibold tracking-tight">
        {psa.name}
      </span>
      <span
        className={`rounded-full px-2 py-0.5 text-[10.5px] font-medium ${
          live
            ? 'bg-brand-tint text-brand-deep dark:bg-brand/30 dark:text-brand-soft'
            : 'bg-[var(--bg)] text-[var(--muted)]'
        }`}
      >
        {STATUS_LABEL[psa.status]}
      </span>
    </span>
  );
}
