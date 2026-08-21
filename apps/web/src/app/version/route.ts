import { readFileSync } from 'node:fs';
import { join } from 'node:path';

/**
 * The deployed bundle's identity. An open dashboard tab is a long-lived SPA — after a deploy it
 * keeps running the old JavaScript with no signal that anything changed, which has cost every
 * verification round a "hard refresh first" instruction. UpdateWatchdog polls this and tells the
 * user when their tab is stale, instead of the tab silently misbehaving.
 *
 * BUILD_ID is read once at module load: it cannot change without a new process, and the standalone
 * Docker bundle ships it alongside the server.
 */
let buildId = 'dev';
try {
  buildId = readFileSync(join(process.cwd(), '.next', 'BUILD_ID'), 'utf8').trim();
} catch {
  // Dev server has no BUILD_ID — 'dev' never differs, so the watchdog stays silent locally.
}

export const dynamic = 'force-dynamic';

export function GET() {
  return Response.json({ buildId }, { headers: { 'cache-control': 'no-store' } });
}
