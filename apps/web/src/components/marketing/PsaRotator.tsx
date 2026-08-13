'use client';

import { useEffect, useState } from 'react';
import { PSA_PLATFORMS } from '@/lib/psaPlatforms';

/**
 * Cycles the PSA name under the hero so the multi-PSA claim is made by the page itself rather
 * than by a sentence about it.
 *
 * Holds still for anyone who asked for reduced motion — a name swapping under its own steam is
 * exactly the kind of movement that setting exists to stop.
 */
export function PsaRotator({ interval = 2600 }: { interval?: number }) {
  const [i, setI] = useState(0);

  useEffect(() => {
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;
    const id = window.setInterval(() => setI((n) => (n + 1) % PSA_PLATFORMS.length), interval);
    return () => window.clearInterval(id);
  }, [interval]);

  const psa = PSA_PLATFORMS[i];

  return (
    <span className="inline-flex min-h-[2rem] items-center">
      <span key={psa.id} className="dp-rise text-[15px] font-semibold tracking-tight">
        {psa.name}
      </span>
    </span>
  );
}
