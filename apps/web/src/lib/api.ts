import { z } from 'zod';
import {
  TicketListItemSchema, TicketDetailSchema, NotificationSchema, ProfileSchema,
  TechnicianResponseSchema, TeamResponseSchema, TrendPointSchema,
  ConnectionSummarySchema, HealthSchema, JobSchema, AuditEntrySchema, AttachmentSchema,
  ConnectionFieldsSchema, FieldOptionSchema, type ConnectionFields,
  MappingRuleSchema, type MappingRule,
  type TicketDetail, type TicketListItem, type Notification, type Profile,
  type TechnicianResponse, type TeamResponse, type TrendPoint,
  type ConnectionSummary, type Health, type Job, type AuditEntry,
} from './types';

// All API calls go through the same-origin BFF proxy, which attaches the bearer token from the
// httpOnly session cookie server-side. No token is ever held in client JavaScript.
const BFF_BASE = '/api/bff';

async function request<T>(path: string, schema: z.ZodType<T>, init?: RequestInit): Promise<T> {
  const res = await fetch(`${BFF_BASE}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      'X-Correlation-ID': crypto.randomUUID(),
      ...(init?.headers ?? {}),
    },
    cache: 'no-store',
  });
  if (res.status === 401) throw new ApiError(401, 'Not authenticated');
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
  updateTicketStatus: (id: string, status: string) =>
    request(`/api/tickets/${id}/status`, z.object({ portalStatus: z.string() }), { method: 'POST', body: JSON.stringify({ status }) }),
  logTime: (id: string, body: { hours: number; billable: string; notes?: string; workType?: string; workRole?: string }) =>
    request(`/api/tickets/${id}/time`,
      z.object({ externalId: z.string().nullable(), timeWorkedHours: z.number(), billableHours: z.number(), nonBillableHours: z.number() }),
      { method: 'POST', body: JSON.stringify(body) }),
  ticketTimeOptions: (id: string) =>
    request(`/api/tickets/${id}/time-options`,
      z.object({ workTypes: z.array(FieldOptionSchema), workRoles: z.array(FieldOptionSchema) })),
  listTimeEntries: (id: string) =>
    request(`/api/tickets/${id}/time`, z.array(TimeEntrySchema)) as Promise<TimeEntry[]>,
  updateTimeEntry: (id: string, entryId: string, body: { hours?: number; billable?: string; notes?: string }) =>
    request(`/api/tickets/${id}/time/${entryId}`, TimeAggregateSchema, { method: 'PUT', body: JSON.stringify(body) }),
  deleteTimeEntry: (id: string, entryId: string) =>
    request(`/api/tickets/${id}/time/${entryId}`, TimeAggregateSchema, { method: 'DELETE' }),
  uploadAttachment: async (ticketId: string, file: File) => {
    const fd = new FormData();
    fd.append('file', file);
    const res = await fetch(`${BFF_BASE}/api/tickets/${ticketId}/attachments`, {
      method: 'POST',
      headers: { 'X-Correlation-ID': crypto.randomUUID() }, // no Content-Type — browser sets multipart boundary
      body: fd,
    });
    if (!res.ok) throw new ApiError(res.status, `upload → ${res.status}`);
    return AttachmentSchema.parse(await res.json());
  },
  attachmentDownloadUrl: (ticketId: string, attachmentId: string) =>
    request(`/api/tickets/${ticketId}/attachments/${attachmentId}/download`, z.object({ url: z.string() })),
  notifications: () => request('/api/notifications', z.array(NotificationSchema)) as Promise<Notification[]>,
  profile: () => request('/api/profile', ProfileSchema) as Promise<Profile>,

  technicianMetrics: () => request('/api/dashboard/technician', TechnicianResponseSchema) as Promise<TechnicianResponse>,
  teamMetrics: (fromIso?: string) =>
    request(`/api/dashboard/team${fromIso ? `?from=${encodeURIComponent(fromIso)}` : ''}`, TeamResponseSchema) as Promise<TeamResponse>,
  trend: (fromIso?: string) =>
    request(`/api/dashboard/trend${fromIso ? `?from=${encodeURIComponent(fromIso)}` : ''}`, z.array(TrendPointSchema)) as Promise<TrendPoint[]>,
  teamExportUrl: `${BFF_BASE}/api/dashboard/team/export`,

  // Admin
  connections: () => request('/api/admin/connections', z.array(ConnectionSummarySchema)) as Promise<ConnectionSummary[]>,
  createConnection: (body: {
    name: string; provider: number; apiEndpoint: string; tenantIdentifier?: string;
    credentials: Record<string, string>; timeZone?: string;
  }) => request('/api/admin/connections', ConnectionSummarySchema, { method: 'POST', body: JSON.stringify(body) }),
  testConnection: (id: string) =>
    request(`/api/admin/connections/${id}/test`,
      z.object({ success: z.boolean(), message: z.string().nullable(), latencyMs: z.number() }),
      { method: 'POST' }),
  syncConnection: (id: string) =>
    request(`/api/admin/connections/${id}/sync`,
      z.object({ fetched: z.number(), created: z.number(), updated: z.number(), skipped: z.number(), pages: z.number() }),
      { method: 'POST' }),
  updateConnection: (id: string, body: {
    name: string; apiEndpoint: string; tenantIdentifier?: string; timeZone?: string;
    isEnabled: boolean; credentials?: Record<string, string>;
  }) => request(`/api/admin/connections/${id}`, ConnectionSummarySchema, { method: 'PUT', body: JSON.stringify(body) }),
  connectionFields: (id: string) =>
    request(`/api/admin/connections/${id}/fields`, ConnectionFieldsSchema) as Promise<ConnectionFields>,
  refreshConnectionFields: (id: string) =>
    request(`/api/admin/connections/${id}/fields/refresh`, ConnectionFieldsSchema, { method: 'POST' }) as Promise<ConnectionFields>,
  listMappings: (provider: number) =>
    request(`/api/admin/mappings?provider=${provider}`, z.array(MappingRuleSchema)) as Promise<MappingRule[]>,
  upsertMapping: (
    body: {
      id?: string; provider: number; scope: number; psaConnectionId: string;
      portalField: string; portalValue: string; externalField: string; externalValue: string;
      direction: number; isRequired: boolean; fallbackValue: string | null;
    },
    note?: string,
  ) => request(`/api/admin/mappings${note ? `?note=${encodeURIComponent(note)}` : ''}`,
    MappingRuleSchema, { method: 'POST', body: JSON.stringify(body) }),
  health: () => request('/api/admin/health', z.array(HealthSchema)) as Promise<Health[]>,
  jobs: (status?: number) =>
    request(`/api/admin/jobs${status != null ? `?status=${status}` : ''}`, z.array(JobSchema)) as Promise<Job[]>,
  reprocessJob: (id: string) =>
    request(`/api/admin/jobs/${id}/reprocess`, z.unknown(), { method: 'POST' }),
  audit: () => request('/api/admin/audit', z.array(AuditEntrySchema)) as Promise<AuditEntry[]>,
};

const TimeEntrySchema = z.object({
  externalId: z.string(),
  hours: z.number(),
  billable: z.boolean(),
  entryDate: z.string(),
  notes: z.string().nullable(),
  technician: z.string(),
});
export type TimeEntry = z.infer<typeof TimeEntrySchema>;

const TimeAggregateSchema = z.object({
  count: z.number(),
  timeWorkedHours: z.number(),
  billableHours: z.number(),
  nonBillableHours: z.number(),
});

const TicketNoteResponse = z.object({
  id: z.string(), authorName: z.string(), authoredByClient: z.boolean(),
  body: z.string(), createdAt: z.string(),
});
