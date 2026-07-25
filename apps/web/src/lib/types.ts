import { z } from 'zod';

export const TicketListItemSchema = z.object({
  id: z.string(),
  externalTicketId: z.string().nullable(),
  provider: z.union([z.string(), z.number()]),
  title: z.string(),
  portalStatus: z.string(),
  portalPriority: z.string(),
  queueOrBoard: z.string().nullable(),
  createdAt: z.string(),
  lastSyncedAt: z.string().nullable(),
});
export type TicketListItem = z.infer<typeof TicketListItemSchema>;

export const TicketNoteSchema = z.object({
  id: z.string(),
  authorName: z.string(),
  authoredByClient: z.boolean(),
  body: z.string(),
  createdAt: z.string(),
});

export const AttachmentSchema = z.object({
  id: z.string(),
  fileName: z.string(),
  contentType: z.string(),
  sizeBytes: z.number(),
  scanStatus: z.union([z.string(), z.number()]),
  uploadedAt: z.string(),
});

export const TicketDetailSchema = z.object({
  id: z.string(),
  externalTicketId: z.string().nullable(),
  provider: z.union([z.string(), z.number()]),
  title: z.string(),
  description: z.string().nullable(),
  portalStatus: z.string(),
  portalPriority: z.string(),
  portalCategory: z.string().nullable(),
  queueOrBoard: z.string().nullable(),
  createdAt: z.string(),
  resolvedAt: z.string().nullable(),
  conversation: z.array(TicketNoteSchema),
  attachments: z.array(AttachmentSchema),
});
export type TicketDetail = z.infer<typeof TicketDetailSchema>;

export const NotificationSchema = z.object({
  ticketId: z.string(),
  title: z.string(),
  kind: z.string(),
  summary: z.string(),
  at: z.string(),
});
export type Notification = z.infer<typeof NotificationSchema>;

export const ScoreContributionSchema = z.object({
  component: z.string(),
  score: z.number(),
  weight: z.number(),
  weightedPoints: z.number(),
});

export const ProductivityScoreSchema = z.object({
  overall: z.number(),
  measuredWeightFraction: z.number(),
  breakdown: z.array(ScoreContributionSchema),
});

export const TechnicianMetricsSchema = z.object({
  technicianExternalId: z.string(),
  assigned: z.number(),
  resolved: z.number(),
  open: z.number(),
  overdue: z.number(),
  slaCompliancePct: z.number(),
  avgResolutionHours: z.number(),
  timeWorkedHours: z.number(),
  billableHours: z.number(),
  nonBillableHours: z.number(),
  score: ProductivityScoreSchema.nullable(),
});

export const TechnicianResponseSchema = z.object({
  metrics: TechnicianMetricsSchema,
  disclaimer: z.string(),
});

export const TeamRowSchema = z.object({
  technicianExternalId: z.string(),
  resolved: z.number(),
  slaCompliancePct: z.number(),
  score: z.number().nullable(),
});
export const TeamResponseSchema = z.object({
  team: z.array(TeamRowSchema),
  disclaimer: z.string(),
});

export const TrendPointSchema = z.object({
  date: z.string(),
  created: z.number(),
  resolved: z.number(),
});
export type TrendPoint = z.infer<typeof TrendPointSchema>;
export type TechnicianResponse = z.infer<typeof TechnicianResponseSchema>;
export type TeamResponse = z.infer<typeof TeamResponseSchema>;

export const ProfileSchema = z.object({
  displayName: z.string().nullable(),
  email: z.string().nullable(),
  clientCompanyId: z.string(),
  isCompanyAdministrator: z.boolean(),
});
export type Profile = z.infer<typeof ProfileSchema>;
