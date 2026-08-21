'use client';

import { useEffect, useRef, useState } from 'react';
import { RefreshCw } from 'lucide-react';

/**
 * Tells the user when their tab is running an outdated bundle. A dashboard tab lives for hours;
 * after a deploy it keeps executing the old JavaScript, and every fix looks "still broken" until
 * someone thinks to hard-refresh. This polls /version (on an interval and whenever the tab regains
 * focus) and shows a reload prompt the moment the deployed BUILD_ID differs from the one this tab
 * booted with.
 *
 * Deliberately a prompt, not an automatic reload: the composer may hold an unsent reply, and no
 * deploy is worth silently discarding a user's text.
 */
const POLL_MS = 3 * 60_000;

export function UpdateWatchdog() {
  const booted = useRef<string | null>(null);
  const [stale, setStale] = useState(false);

  useEffect(() => {
    let stop = false;
    async function check() {
      try {
        const res = await fetch('/version', { cache: 'no-store' });
        if (!res.ok) return;
        const { buildId } = (await res.json()) as { buildId?: string };
        if (!buildId || stop) return;
        if (booted.current === null) booted.current = buildId;
        else if (buildId !== booted.current) setStale(true);
      } catch {
        // Offline or mid-deploy — try again next tick.
      }
    }
    check();
    const timer = setInterval(check, POLL_MS);
    const onFocus = () => check();
    window.addEventListener('focus', onFocus);
    return () => { stop = true; clearInterval(timer); window.removeEventListener('focus', onFocus); };
  }, []);

  if (!stale) return null;
  return (
    <div className="fixed bottom-4 left-1/2 z-50 -translate-x-1/2">
      <div className="flex items-center gap-3 rounded-xl border border-[var(--border)] bg-[var(--surface)] px-4 py-2.5 shadow-lg">
        <span className="text-sm">A new version of the portal is available.</span>
        <button type="button" onClick={() => window.location.reload()}
          className="inline-flex items-center gap-1.5 rounded-lg bg-brand px-3 py-1.5 text-xs font-medium text-brand-fg hover:opacity-90">
          <RefreshCw size={13} /> Reload
        </button>
      </div>
    </div>
  );
}
