import { z } from 'zod';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE ?? 'http://localhost:5080';

export const MeSchema = z.object({
  subject: z.string().nullable(),
  email: z.string().nullable(),
  displayName: z.string().nullable(),
  organizationId: z.string().uuid().nullable(),
  isPlatformScope: z.boolean(),
  permissions: z.array(z.string()),
});
export type Me = z.infer<typeof MeSchema>;

/** Typed fetch against the Desk API. Sends the bearer token and correlation id. */
export async function apiGet<T>(path: string, schema: z.ZodType<T>, token?: string): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    headers: {
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      'X-Correlation-ID': crypto.randomUUID(),
    },
    cache: 'no-store',
  });
  if (!res.ok) throw new Error(`API ${path} failed: ${res.status}`);
  return schema.parse(await res.json());
}

export const getMe = (token?: string) => apiGet('/api/me', MeSchema, token);
