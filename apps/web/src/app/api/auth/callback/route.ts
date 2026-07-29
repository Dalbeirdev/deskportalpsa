import { NextRequest, NextResponse } from 'next/server';
import { authConfig, oidc, cookies as ck, isProd } from '@/lib/authConfig';

// Completes the flow: verify state, exchange the code (+ PKCE verifier) for tokens, and store them
// in httpOnly cookies. Tokens never reach client-side JavaScript.
export async function GET(req: NextRequest) {
  const url = new URL(req.url);
  const code = url.searchParams.get('code');
  const state = url.searchParams.get('state');
  const savedState = req.cookies.get(ck.state)?.value;
  const verifier = req.cookies.get(ck.verifier)?.value;

  const fail = (reason: string) =>
    NextResponse.redirect(`${authConfig.appUrl}/?error=${encodeURIComponent(reason)}`);

  if (!code || !state || !savedState || state !== savedState || !verifier) return fail('invalid_state');

  const body = new URLSearchParams({
    grant_type: 'authorization_code',
    code,
    redirect_uri: oidc.redirectUri,
    client_id: authConfig.clientId,
    code_verifier: verifier,
    ...(authConfig.clientSecret ? { client_secret: authConfig.clientSecret } : {}),
  });

  const tokenRes = await fetch(oidc.token, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body,
    cache: 'no-store',
  });
  if (!tokenRes.ok) return fail('token_exchange_failed');

  const tokens = (await tokenRes.json()) as {
    access_token: string; refresh_token?: string; id_token?: string; expires_in?: number;
  };

  const res = NextResponse.redirect(`${authConfig.appUrl}/dashboard`);
  const secure = isProd;
  res.cookies.delete(ck.verifier);
  res.cookies.delete(ck.state);
  res.cookies.set(ck.access, tokens.access_token, {
    httpOnly: true, secure, sameSite: 'lax', path: '/', maxAge: tokens.expires_in ?? 300,
  });
  if (tokens.refresh_token)
    res.cookies.set(ck.refresh, tokens.refresh_token, {
      httpOnly: true, secure, sameSite: 'lax', path: '/', maxAge: 60 * 60 * 24 * 7,
    });
  if (tokens.id_token)
    res.cookies.set(ck.idToken, tokens.id_token, { httpOnly: true, secure, sameSite: 'lax', path: '/' });
  return res;
}
