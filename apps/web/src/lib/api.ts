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
  if (!res.ok) {
    // Surface the API's problem-details message when present so users see the real reason
    // (e.g. which statuses the PSA's board actually allows), not just a status code.
    let detail: string | null = null;
    try {
      const body = await res.json();
      detail = body?.detail ?? body?.title ?? null;
    } catch { /* non-JSON body */ }
    throw new ApiError(res.status, detail ?? `${init?.method ?? 'GET'} ${path} → ${res.status}`);
  }
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

  // Client control panel (CP-1)
  cpCapabilities: () => request('/api/control-panel/capabilities', CapabilitiesSchema) as Promise<Capabilities>,
  cpInstructions: () => request('/api/control-panel/instructions', InstructionsViewSchema) as Promise<InstructionsView>,
  cpSaveInstruction: (clientCompanyId: string | null, body: string) =>
    request('/api/control-panel/instructions', InstructionSchema,
      { method: 'PUT', body: JSON.stringify({ clientCompanyId, body }) }) as Promise<Instruction>,
  cpUsers: () => request('/api/control-panel/users', z.array(ClientUserSchema)) as Promise<ClientUser[]>,
  cpInviteUser: (body: { email: string; displayName: string; isCompanyAdministrator: boolean }) =>
    request('/api/control-panel/users', ClientUserSchema, { method: 'POST', body: JSON.stringify(body) }) as Promise<ClientUser>,
  cpSetUserActive: (id: string, active: boolean) =>
    request(`/api/control-panel/users/${id}/active`, z.unknown(), { method: 'POST', body: JSON.stringify({ active }) }),
  cpSetUserAccess: (id: string, body: { isCompanyAdministrator: boolean; grants: { section: string; clientCompanyId: string | null }[] }) =>
    request(`/api/control-panel/users/${id}/access`, ClientUserSchema, { method: 'PUT', body: JSON.stringify(body) }) as Promise<ClientUser>,

  // Control panel — per-account settings (CP-2)
  cpAccount: () => request('/api/control-panel/account', AccountSchema) as Promise<Account>,
  cpApprovers: () => request('/api/control-panel/approvers', z.array(ApproverSchema)) as Promise<Approver[]>,
  cpSaveApprover: (body: ApproverInput) => request('/api/control-panel/approvers', ApproverSchema, { method: 'PUT', body: JSON.stringify(body) }) as Promise<Approver>,
  cpDeleteApprover: (id: string) => request(`/api/control-panel/approvers/${id}`, z.unknown(), { method: 'DELETE' }),
  cpEscalation: () => request('/api/control-panel/escalation', z.array(EscalationSchema)) as Promise<EscalationLevel[]>,
  cpSaveEscalation: (body: EscalationInput) => request('/api/control-panel/escalation', EscalationSchema, { method: 'PUT', body: JSON.stringify(body) }) as Promise<EscalationLevel>,
  cpDeleteEscalation: (id: string) => request(`/api/control-panel/escalation/${id}`, z.unknown(), { method: 'DELETE' }),
  cpHolidays: () => request('/api/control-panel/holidays', z.array(HolidaySchema)) as Promise<Holiday[]>,
  cpSaveHoliday: (body: HolidayInput) => request('/api/control-panel/holidays', HolidaySchema, { method: 'PUT', body: JSON.stringify(body) }) as Promise<Holiday>,
  cpDeleteHoliday: (id: string) => request(`/api/control-panel/holidays/${id}`, z.unknown(), { method: 'DELETE' }),
  cpDevices: () => request('/api/control-panel/devices', z.array(DeviceSchema)) as Promise<Device[]>,
  cpSaveDevice: (body: DeviceInput) => request('/api/control-panel/devices', DeviceSchema, { method: 'PUT', body: JSON.stringify(body) }) as Promise<Device>,
  cpDeleteDevice: (id: string) => request(`/api/control-panel/devices/${id}`, z.unknown(), { method: 'DELETE' }),
  cpBusinessHours: () => request('/api/control-panel/business-hours', BusinessHoursSchema) as Promise<BusinessHours>,
  cpSaveBusinessHours: (body: BusinessHours) => request('/api/control-panel/business-hours', BusinessHoursSchema, { method: 'PUT', body: JSON.stringify(body) }) as Promise<BusinessHours>,
};

// ---- Control panel account-settings schemas (CP-2) ----
const AccountSchema = z.object({ id: z.string(), name: z.string(), externalCompanyId: z.string(), connectionName: z.string().nullable(), isActive: z.boolean() });
export type Account = z.infer<typeof AccountSchema>;

const ApproverSchema = z.object({ id: z.string(), name: z.string(), email: z.string().nullable(), phone: z.string().nullable(), scope: z.string().nullable(), sortOrder: z.number() });
export type Approver = z.infer<typeof ApproverSchema>;
export type ApproverInput = { id?: string; name: string; email?: string | null; phone?: string | null; scope?: string | null; sortOrder: number };

const EscalationSchema = z.object({ id: z.string(), level: z.number(), name: z.string(), contact: z.string().nullable(), condition: z.string().nullable() });
export type EscalationLevel = z.infer<typeof EscalationSchema>;
export type EscalationInput = { id?: string; level: number; name: string; contact?: string | null; condition?: string | null };

const HolidaySchema = z.object({ id: z.string(), date: z.string(), name: z.string() });
export type Holiday = z.infer<typeof HolidaySchema>;
export type HolidayInput = { id?: string; date: string; name: string };

const DeviceSchema = z.object({ id: z.string(), name: z.string(), type: z.string().nullable(), identifier: z.string().nullable(), notes: z.string().nullable() });
export type Device = z.infer<typeof DeviceSchema>;
export type DeviceInput = { id?: string; name: string; type?: string | null; identifier?: string | null; notes?: string | null };

const BusinessHoursSchema = z.object({ timeZone: z.string().nullable(), scheduleJson: z.string(), notes: z.string().nullable() });
export type BusinessHours = z.infer<typeof BusinessHoursSchema>;

// ---- Control panel schemas ----
const CapabilitiesSchema = z.object({
  isCompanyAdministrator: z.boolean(),
  clientCompanyId: z.string(),
  companyName: z.string(),
  sections: z.array(z.string()),
});
export type Capabilities = z.infer<typeof CapabilitiesSchema>;

const InstructionSchema = z.object({
  clientCompanyId: z.string().nullable(),
  scope: z.string(),
  accountName: z.string(),
  body: z.string(),
  lastEditedBy: z.string().nullable(),
  updatedAt: z.string().nullable(),
});
export type Instruction = z.infer<typeof InstructionSchema>;

const InstructionsViewSchema = z.object({
  global: InstructionSchema,
  accounts: z.array(InstructionSchema),
});
export type InstructionsView = z.infer<typeof InstructionsViewSchema>;

const AccessGrantSchema = z.object({ section: z.string(), clientCompanyId: z.string().nullable() });
const ClientUserSchema = z.object({
  id: z.string(),
  email: z.string(),
  displayName: z.string(),
  isCompanyAdministrator: z.boolean(),
  isActive: z.boolean(),
  grants: z.array(AccessGrantSchema),
});
export type ClientUser = z.infer<typeof ClientUserSchema>;

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
