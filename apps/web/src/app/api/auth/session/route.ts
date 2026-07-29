import { NextRequest, NextResponse } from 'next/server';
import { cookies as ck } from '@/lib/authConfig';
import { decodeJwtPayload } from '@/lib/pkce';

// Lightweight session probe for the UI. Reads display claims from the access token; the API is the
// authority that actually verifies the token signature on every request.
export async function GET(req: NextRequest) {
  const at = req.cookies.get(ck.access)?.value;
  if (!at) return NextResponse.json({ authenticated: false });

  const claims = decodeJwtPayload(at);
  return NextResponse.json({
    authenticated: true,
    name: (claims?.name ?? claims?.preferred_username ?? null) as string | null,
    email: (claims?.email ?? null) as string | null,
  });
}
