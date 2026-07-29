import { createHash, randomBytes } from 'node:crypto';

/** base64url without padding, per RFC 7636. */
function base64url(input: Buffer): string {
  return input.toString('base64').replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

/** A high-entropy PKCE code verifier. */
export function createVerifier(): string {
  return base64url(randomBytes(32));
}

/** The S256 code challenge for a verifier. */
export function challengeFor(verifier: string): string {
  return base64url(createHash('sha256').update(verifier).digest());
}

/** An opaque anti-CSRF state / nonce value. */
export function randomToken(): string {
  return base64url(randomBytes(16));
}

/** Decode a JWT payload without verifying (display only; the API verifies signatures). */
export function decodeJwtPayload(token: string): Record<string, unknown> | null {
  const parts = token.split('.');
  if (parts.length < 2) return null;
  try {
    const json = Buffer.from(parts[1].replace(/-/g, '+').replace(/_/g, '/'), 'base64').toString('utf8');
    return JSON.parse(json);
  } catch {
    return null;
  }
}
