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

export const ProfileSchema = z.object({
  displayName: z.string().nullable(),
  email: z.string().nullable(),
  clientCompanyId: z.string(),
  isCompanyAdministrator: z.boolean(),
});
export type Profile = z.infer<typeof ProfileSchema>;
