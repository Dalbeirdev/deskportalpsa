import { NextRequest, NextResponse } from 'next/server';
import { authConfig, oidc, cookies as ck, isProd } from '@/lib/authConfig';
import { createVerifier, challengeFor, randomToken } from '@/lib/pkce';

// Starts the OIDC authorization-code + PKCE flow: stash the verifier + state in short-lived
// httpOnly cookies and redirect the user to Keycloak.
export async function GET(_req: NextRequest) {
  const verifier = createVerifier();
  const state = randomToken();

  const params = new URLSearchParams({
    response_type: 'code',
    client_id: authConfig.clientId,
    redirect_uri: oidc.redirectUri,
    scope: authConfig.scope,
    state,
    code_challenge: challengeFor(verifier),
    code_challenge_method: 'S256',
  });

  const res = NextResponse.redirect(`${oidc.authorize}?${params.toString()}`);
  const opts = { httpOnly: true, secure: isProd, sameSite: 'lax' as const, path: '/', maxAge: 600 };
  res.cookies.set(ck.verifier, verifier, opts);
  res.cookies.set(ck.state, state, opts);
  return res;
}
