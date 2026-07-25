import { z } from 'zod';
import {
  TicketListItemSchema, TicketDetailSchema, NotificationSchema, ProfileSchema,
  TechnicianResponseSchema, TeamResponseSchema, TrendPointSchema,
  ConnectionSummarySchema, HealthSchema, JobSchema, AuditEntrySchema, AttachmentSchema,
  type TicketDetail, type TicketListItem, type Notification, type Profile,
  type TechnicianResponse, type TeamResponse, type TrendPoint,
  type ConnectionSummary, type Health, type Job, type AuditEntry,
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
  if (res.status === 204 || res.headers.get('content-length') === '0') return undefined as T;
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
  uploadAttachment: async (ticketId: string, file: File) => {
    const t = token();
    const fd = new FormData();
    fd.append('file', file);
    const res = await fetch(`${API_BASE}/api/tickets/${ticketId}/attachments`, {
      method: 'POST',
      headers: { ...(t ? { Authorization: `Bearer ${t}` } : {}), 'X-Correlation-ID': crypto.randomUUID() },
      body: fd, // no Content-Type — the browser sets the multipart boundary
    });
    if (!res.ok) throw new ApiError(res.status, `upload → ${res.status}`);
    return AttachmentSchema.parse(await res.json());
  },
  attachmentDownloadUrl: (ticketId: string, attachmentId: string) =>
    request(`/api/tickets/${ticketId}/attachments/${attachmentId}/download`, z.object({ url: z.string() })),
  notifications: () => request('/api/notifications', z.array(NotificationSchema)) as Promise<Notification[]>,
  profile: () => request('/api/profile', ProfileSchema) as Promise<Profile>,

  technicianMetrics: () => request('/api/dashboard/technician', TechnicianResponseSchema) as Promise<TechnicianResponse>,
  teamMetrics: () => request('/api/dashboard/team', TeamResponseSchema) as Promise<TeamResponse>,
  trend: () => request('/api/dashboard/trend', z.array(TrendPointSchema)) as Promise<TrendPoint[]>,
  teamExportUrl: `${API_BASE}/api/dashboard/team/export`,

  // Admin
  connections: () => request('/api/admin/connections', z.array(ConnectionSummarySchema)) as Promise<ConnectionSummary[]>,
  createConnection: (body: {
    name: string; provider: number; apiEndpoint: string; tenantIdentifier?: string;
    credentials: Record<string, string>; timeZone?: string;
  }) => request('/api/admin/connections', ConnectionSummarySchema, { method: 'POST', body: JSON.stringify(body) }),
  health: () => request('/api/admin/health', z.array(HealthSchema)) as Promise<Health[]>,
  jobs: (status?: number) =>
    request(`/api/admin/jobs${status != null ? `?status=${status}` : ''}`, z.array(JobSchema)) as Promise<Job[]>,
  reprocessJob: (id: string) =>
    request(`/api/admin/jobs/${id}/reprocess`, z.unknown(), { method: 'POST' }),
  audit: () => request('/api/admin/audit', z.array(AuditEntrySchema)) as Promise<AuditEntry[]>,
};

const TicketNoteResponse = z.object({
  id: z.string(), authorName: z.string(), authoredByClient: z.boolean(),
  body: z.string(), createdAt: z.string(),
});
