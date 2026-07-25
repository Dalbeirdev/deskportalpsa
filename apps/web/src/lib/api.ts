import { z } from 'zod';
import {
  TicketListItemSchema, TicketDetailSchema, NotificationSchema, ProfileSchema,
  TechnicianResponseSchema, TeamResponseSchema, TrendPointSchema,
  type TicketDetail, type TicketListItem, type Notification, type Profile,
  type TechnicianResponse, type TeamResponse, type TrendPoint,
} from './types';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE ?? 'http://localhost:5080';

/** Reads the access token a real login flow would have stored. Absent in this build. */
function token(): string | undefined {
  if (typeof window === 'undefined') return undefined;
  return window.localStorage.getItem('desk-token') ?? undefined;
}

async function request<T>(path: string, schema: z.ZodType<T>, init?: RequestInit): Promise<T> {
  const t = token();
  const res = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(t ? { Authorization: `Bearer ${t}` } : {}),
      'X-Correlation-ID': crypto.randomUUID(),
      ...(init?.headers ?? {}),
    },
    cache: 'no-store',
  });
  if (!res.ok) throw new ApiError(res.status, `${init?.method ?? 'GET'} ${path} → ${res.status}`);
  return schema.parse(await res.json());
}

export class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
  }
}

export const MeSchema = z.object({
  subject: z.string().nullable(),
  email: z.string().nullable(),
  displayName: z.string().nullable(),
  organizationId: z.string().nullable(),
  isPlatformScope: z.boolean(),
  permissions: z.array(z.string()),
});

export const api = {
  listTickets: () => request('/api/tickets', z.array(TicketListItemSchema)) as Promise<TicketListItem[]>,
  getTicket: (id: string) => request(`/api/tickets/${id}`, TicketDetailSchema) as Promise<TicketDetail>,
  createTicket: (body: { title: string; description?: string; priority?: string; queueOrBoard?: string }) =>
    request('/api/tickets', z.object({ id: z.string(), externalTicketId: z.string().nullable() }), {
      method: 'POST',
      body: JSON.stringify(body),
    }),
  addComment: (id: string, body: string) =>
    request(`/api/tickets/${id}/comments`, TicketNoteResponse, { method: 'POST', body: JSON.stringify({ body }) }),
  notifications: () => request('/api/notifications', z.array(NotificationSchema)) as Promise<Notification[]>,
  profile: () => request('/api/profile', ProfileSchema) as Promise<Profile>,

  technicianMetrics: () => request('/api/dashboard/technician', TechnicianResponseSchema) as Promise<TechnicianResponse>,
  teamMetrics: () => request('/api/dashboard/team', TeamResponseSchema) as Promise<TeamResponse>,
  trend: () => request('/api/dashboard/trend', z.array(TrendPointSchema)) as Promise<TrendPoint[]>,
  teamExportUrl: `${API_BASE}/api/dashboard/team/export`,
};

const TicketNoteResponse = z.object({
  id: z.string(), authorName: z.string(), authoredByClient: z.boolean(),
  body: z.string(), createdAt: z.string(),
});
