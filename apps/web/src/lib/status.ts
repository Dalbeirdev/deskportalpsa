// Shared status classification. Tickets synced before their statuses are mapped carry the RAW PSA
// value (e.g. ConnectWise "New (not responded)"), so classification must tolerate both the portal-
// neutral enums and raw provider strings rather than assuming a closed set.
export function isResolvedStatus(status: string): boolean {
  const s = status.toUpperCase();
  return s.includes('RESOLV') || s.includes('CLOSED');
}
