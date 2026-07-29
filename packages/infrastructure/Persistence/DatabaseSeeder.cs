using Desk.Domain.Audit;
using Desk.Domain.Authorization;
using Desk.Domain.Enums;
using Desk.Domain.Identity;
using Desk.Domain.Mapping;
using Desk.Domain.Sync;
using Desk.Domain.Tenancy;
using Desk.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace Desk.Infrastructure.Persistence;

/// <summary>Seeds the seven built-in system roles with their default permission claim sets.</summary>
public static class DatabaseSeeder
{
    public static async Task SeedBuiltInRolesAsync(DeskDbContext db, CancellationToken ct = default)
    {
        foreach (var roleType in Enum.GetValues<RoleType>())
        {
            var exists = await db.Roles
                .IgnoreQueryFilters()
                .AnyAsync(r => r.IsSystemRole && r.BuiltInType == roleType, ct);
            if (exists) continue;

            var role = new Role
            {
                Name = roleType.ToString(),
                BuiltInType = roleType,
                IsSystemRole = true,
                MspOrganizationId = null,
            };
            foreach (var perm in Permissions.ForRole(roleType))
                role.Permissions.Add(new RolePermission { PermissionKey = perm });

            db.Roles.Add(role);
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Local-mode subject the dev auto-login authenticates as.</summary>
    public const string DevAdminSubject = "dev-admin";

    /// <summary>
    /// Local/demo seed. Builds a fully populated tenant so every page renders live data without any
    /// external PSA or Keycloak: a demo MSP org, the dev-login user (who is BOTH the MSP admin and a
    /// client-company administrator, so the client-portal and staff endpoints both resolve), a PSA
    /// connection, a client company, a fleet of tickets across technicians/statuses/priorities that
    /// the dashboards aggregate, plus notes, sync events, background jobs and audit entries. Only
    /// invoked in local mode; runs under platform scope so tenant stamps are taken from the rows.
    /// </summary>
    public static async Task SeedLocalDemoAsync(DeskDbContext db, CancellationToken ct = default)
    {
        var org = await db.MspOrganizations.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Slug == "demo", ct);
        if (org is null)
        {
            org = new MspOrganization { Name = "Demo MSP", Slug = "demo" };
            db.MspOrganizations.Add(org);
            await db.SaveChangesAsync(ct);
        }

        var staff = await db.AppUsers.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.IdpSubject == DevAdminSubject, ct);
        if (staff is null)
        {
            staff = new AppUser
            {
                MspOrganizationId = org.Id,
                Email = "dev-admin@local",
                DisplayName = "Demo Admin",
                IdpSubject = DevAdminSubject,
            };
            db.AppUsers.Add(staff);
            await db.SaveChangesAsync(ct);

            var mspAdmin = await db.Roles.IgnoreQueryFilters()
                .FirstAsync(r => r.IsSystemRole && r.BuiltInType == RoleType.MspAdministrator, ct);
            db.UserRoles.Add(new UserRole { AppUserId = staff.Id, RoleId = mspAdmin.Id });
            await db.SaveChangesAsync(ct);
        }

        // Everything below is demo content. Guard on the connection so re-running is a no-op.
        if (await db.PsaConnections.IgnoreQueryFilters().AnyAsync(ct)) return;

        var connection = new PsaConnection
        {
            MspOrganizationId = org.Id,
            Name = "TechPio ConnectWise",
            Provider = ProviderType.ConnectWisePsa,
            ApiEndpoint = "https://staging.connectwisedev.com/v4_6_release/apis/3.0/",
            TenantIdentifier = "techpio",
            CredentialSecretRef = "local/demo/connectwise",
            TimeZone = "America/New_York",
            Status = ConnectionStatus.Healthy,
            IsEnabled = true,
            LastSuccessfulSyncAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            LastHealthCheckAt = DateTimeOffset.UtcNow.AddMinutes(-2),
        };
        db.PsaConnections.Add(connection);

        var company = new ClientCompany
        {
            MspOrganizationId = org.Id,
            PsaConnectionId = connection.Id,
            Name = "Acme Corporation",
            ExternalCompanyId = "acme_co",
            IsActive = true,
        };
        db.ClientCompanies.Add(company);

        // The dev subject is ALSO a client-company administrator, so /api/tickets, /api/notifications
        // and /api/profile resolve for the same auto-login user that drives the admin dashboards.
        var client = new ClientUser
        {
            MspOrganizationId = org.Id,
            ClientCompanyId = company.Id,
            Email = "dev-admin@local",
            DisplayName = "Demo Admin",
            IdpSubject = DevAdminSubject,
            ExternalContactId = "acme_contact_1",
            IsCompanyAdministrator = true,
            IsActive = true,
        };
        db.ClientUsers.Add(client);
        await db.SaveChangesAsync(ct);

        SeedTickets(db, org.Id, connection.Id, company.Id, client.Id);
        SeedOps(db, org.Id, connection.Id);
        SeedMappings(db, org.Id, connection.Id);
        await db.SaveChangesAsync(ct);
    }

    // ---- Field mappings -------------------------------------------------------------------------

    private static readonly (string Field, (string Portal, string External)[] Pairs)[] MappingSets =
    {
        ("status", new[]
        {
            ("NEW", "New (Not Responded)"), ("IN_PROGRESS", "In Progress"),
            ("WAITING_CUSTOMER", "Waiting on Customer"), ("ON_HOLD", "On Hold"),
            ("RESOLVED", "Resolved"), ("CLOSED", "Closed"),
        }),
        ("priority", new[]
        {
            ("CRITICAL", "Priority 1 - Emergency"), ("HIGH", "Priority 2 - High"),
            ("NORMAL", "Priority 3 - Medium"), ("LOW", "Priority 4 - Low"),
        }),
        ("queue", new[]
        {
            ("Help Desk", "Service Desk"), ("Network", "Network Operations"),
            ("Projects", "Professional Services"),
        }),
        ("category", new[]
        {
            ("Hardware", "Hardware"), ("Software", "Software"),
            ("Network", "Network"), ("Access", "Account / Access"),
        }),
    };

    private static void SeedMappings(DeskDbContext db, Guid orgId, Guid connId)
    {
        foreach (var (field, pairs) in MappingSets)
            foreach (var (portal, external) in pairs)
                db.FieldMappings.Add(new FieldMapping
                {
                    MspOrganizationId = orgId,
                    Provider = ProviderType.ConnectWisePsa,
                    Scope = MappingScope.ConnectionOverride,
                    PsaConnectionId = connId,
                    PortalField = field,
                    PortalValue = portal,
                    ExternalField = field,
                    ExternalValue = external,
                    Direction = MappingDirection.Bidirectional,
                    IsActive = true,
                });
    }

    // ---- Demo tickets ---------------------------------------------------------------------------

    private sealed record TechPlan(string Name, int Resolved, int Open, int OverSla, double AvgHours);

    private static readonly TechPlan[] Techs =
    {
        new("John Doe", Resolved: 9, Open: 2, OverSla: 0, AvgHours: 2.8),
        new("Sarah Lee", Resolved: 8, Open: 3, OverSla: 1, AvgHours: 3.3),
        new("Mike Smith", Resolved: 7, Open: 2, OverSla: 1, AvgHours: 3.7),
        new("David Brown", Resolved: 6, Open: 2, OverSla: 1, AvgHours: 4.1),
        new("Emily Davis", Resolved: 5, Open: 3, OverSla: 1, AvgHours: 4.4),
    };

    private static readonly string[] Titles =
    {
        "Printer not responding", "VPN connection issue", "Microsoft 365 login problem",
        "Email not syncing", "Outlook not opening", "Slow laptop performance",
        "Password reset request", "New user onboarding", "Shared drive access denied",
        "Wi-Fi keeps dropping", "Software installation request", "Two-factor setup help",
        "Monitor flickering", "Phone system down", "Backup job failed",
        "Firewall rule change", "Disk almost full", "Application crashing on launch",
    };
    private static readonly string[] Queues = { "Service Desk", "Network Operations", "Professional Services" };
    private static readonly string[] Priorities = { "LOW", "NORMAL", "NORMAL", "HIGH", "HIGH", "CRITICAL" };
    private static readonly string[] Categories = { "Hardware", "Software", "Network", "Access", "Email" };
    private static readonly string[] OpenStatuses = { "NEW", "IN_PROGRESS", "WAITING_CUSTOMER", "ON_HOLD" };
    private static readonly (string Name, string Email)[] Requesters =
    {
        ("John Carter", "jcarter@acme.example"), ("Maria Gomez", "mgomez@acme.example"),
        ("Liam Chen", "lchen@acme.example"), ("Priya Patel", "ppatel@acme.example"),
        ("Tom Becker", "tbecker@acme.example"),
    };

    private static void SeedTickets(DeskDbContext db, Guid orgId, Guid connId, Guid companyId, Guid clientUserId)
    {
        var now = DateTimeOffset.UtcNow;
        var seq = 1000;
        var idx = 0;

        foreach (var tech in Techs)
        {
            for (var i = 0; i < tech.Resolved; i++, idx++)
            {
                seq++;
                // Spread creation across the last 7 days so the trend chart has daily buckets.
                var created = now.AddDays(-(idx % 7) - 1).AddHours(-(idx % 5));
                var overSla = i < tech.OverSla;
                var resolutionHours = overSla ? tech.AvgHours + 2.5 : Math.Max(1.0, tech.AvgHours - (i % 3) * 0.4);
                var resolved = created.AddHours(resolutionHours);
                var slaDue = created.AddHours(4); // 4h SLA target; within unless it took longer
                var worked = Math.Round((decimal)resolutionHours - 0.3m, 2);
                var billable = Math.Round(worked * 0.8m, 2);
                var req = Requesters[idx % Requesters.Length];

                var ticket = new Ticket
                {
                    MspOrganizationId = orgId,
                    PsaConnectionId = connId,
                    Provider = ProviderType.ConnectWisePsa,
                    ExternalTicketId = "T-" + seq,
                    ClientCompanyId = companyId,
                    RequesterUserId = clientUserId,
                    RequesterName = req.Name,
                    RequesterEmail = req.Email,
                    Title = Titles[idx % Titles.Length],
                    Description = "Reported via the client portal.",
                    PortalStatus = i % 4 == 0 ? "CLOSED" : "RESOLVED",
                    PsaStatus = i % 4 == 0 ? "Closed" : "Resolved",
                    PortalPriority = Priorities[idx % Priorities.Length],
                    PortalCategory = Categories[idx % Categories.Length],
                    QueueOrBoard = Queues[idx % Queues.Length],
                    AssignedTechnicianExternalId = tech.Name,
                    SlaDueAt = slaDue,
                    ResolvedAt = resolved,
                    ClosedAt = i % 4 == 0 ? resolved.AddHours(1) : null,
                    TimeWorkedHours = worked,
                    BillableHours = billable,
                    NonBillableHours = Math.Max(0m, worked - billable),
                    SyncStatus = TicketSyncStatus.Synced,
                    LastSyncedAt = resolved.AddMinutes(5),
                    CreatedAt = created,
                };
                db.Tickets.Add(ticket);
                db.TicketNotes.Add(new TicketNote
                {
                    MspOrganizationId = orgId,
                    TicketId = ticket.Id,
                    AuthorName = tech.Name,
                    AuthoredByClient = false,
                    Body = "Resolved: applied fix and verified with the customer.",
                    IsPublic = true,
                    NoteCreatedAt = resolved,
                    CreatedAt = resolved,
                });
            }

            for (var i = 0; i < tech.Open; i++, idx++)
            {
                seq++;
                var created = now.AddDays(-(idx % 4)).AddHours(-(idx % 6));
                var overdue = i == 0; // one overdue per tech
                var slaDue = overdue ? now.AddHours(-3) : now.AddHours(6);
                var req = Requesters[idx % Requesters.Length];

                var ticket = new Ticket
                {
                    MspOrganizationId = orgId,
                    PsaConnectionId = connId,
                    Provider = ProviderType.ConnectWisePsa,
                    ExternalTicketId = "T-" + seq,
                    ClientCompanyId = companyId,
                    RequesterUserId = clientUserId,
                    RequesterName = req.Name,
                    RequesterEmail = req.Email,
                    Title = Titles[idx % Titles.Length],
                    Description = "Reported via the client portal.",
                    PortalStatus = OpenStatuses[i % OpenStatuses.Length],
                    PsaStatus = OpenStatuses[i % OpenStatuses.Length],
                    PortalPriority = Priorities[idx % Priorities.Length],
                    PortalCategory = Categories[idx % Categories.Length],
                    QueueOrBoard = Queues[idx % Queues.Length],
                    AssignedTechnicianExternalId = tech.Name,
                    SlaDueAt = slaDue,
                    TimeWorkedHours = i * 0.5m,
                    SyncStatus = TicketSyncStatus.Synced,
                    LastSyncedAt = created.AddMinutes(10),
                    CreatedAt = created,
                };
                db.Tickets.Add(ticket);
            }
        }
    }

    // ---- Sync events, background jobs, audit ----------------------------------------------------

    private static void SeedOps(DeskDbContext db, Guid orgId, Guid connId)
    {
        var now = DateTimeOffset.UtcNow;

        // Sync events: mostly processed; one failed so Integration Health shows a failed event.
        for (var i = 0; i < 8; i++)
        {
            db.SyncEvents.Add(new SyncEvent
            {
                MspOrganizationId = orgId,
                PsaConnectionId = connId,
                EventType = i % 2 == 0 ? "ticket.updated" : "ticket.created",
                IdempotencyKey = "evt-" + i,
                SourceMarker = "provider",
                OccurredAt = now.AddMinutes(-i * 7),
                Processed = true,
                CreatedAt = now.AddMinutes(-i * 7),
            });
        }
        db.SyncEvents.Add(new SyncEvent
        {
            MspOrganizationId = orgId,
            PsaConnectionId = connId,
            EventType = "ticket.updated",
            IdempotencyKey = "evt-failed-1",
            SourceMarker = "provider",
            OccurredAt = now.AddMinutes(-18),
            Processed = false,
            Error = "Mapping not found for PSA status 'Escalated'.",
            CreatedAt = now.AddMinutes(-18),
        });

        // Background jobs: two queued (pending), most succeeded, one dead-lettered.
        db.BackgroundJobs.Add(new BackgroundJob
        {
            MspOrganizationId = orgId, JobType = "OutboundTicketSync", PayloadJson = "{\"ticket\":\"T-1042\"}",
            Status = BackgroundJobStatus.Queued, Attempts = 0, NextAttemptAt = now.AddMinutes(1), CreatedAt = now.AddMinutes(-3),
        });
        db.BackgroundJobs.Add(new BackgroundJob
        {
            MspOrganizationId = orgId, JobType = "InboundPoll", PayloadJson = "{\"connection\":\"connectwise\"}",
            Status = BackgroundJobStatus.Queued, Attempts = 0, NextAttemptAt = now.AddMinutes(2), CreatedAt = now.AddMinutes(-1),
        });
        for (var i = 0; i < 5; i++)
            db.BackgroundJobs.Add(new BackgroundJob
            {
                MspOrganizationId = orgId, JobType = "OutboundTicketSync", PayloadJson = "{}",
                Status = BackgroundJobStatus.Succeeded, Attempts = 1, CreatedAt = now.AddMinutes(-30 - i * 5),
            });
        db.BackgroundJobs.Add(new BackgroundJob
        {
            MspOrganizationId = orgId, JobType = "AttachmentScan", PayloadJson = "{\"file\":\"invoice.pdf\"}",
            Status = BackgroundJobStatus.DeadLettered, Attempts = 5, MaxAttempts = 5,
            LastError = "Scanner timed out after 5 attempts.", CreatedAt = now.AddHours(-2),
        });

        // Audit trail (append-only; must carry the org id to appear in the tenant-scoped query).
        (string action, string entity, string actor, DateTimeOffset when)[] audits =
        {
            ("connection.tested", "PsaConnection", "Demo Admin", now.AddMinutes(-4)),
            ("mapping.updated", "FieldMapping", "Demo Admin", now.AddMinutes(-40)),
            ("ticket.created", "Ticket", "John Carter", now.AddMinutes(-55)),
            ("clientuser.invited", "ClientUser", "Demo Admin", now.AddHours(-2)),
            ("connection.created", "PsaConnection", "Demo Admin", now.AddHours(-3)),
        };
        foreach (var (action, entity, actor, when) in audits)
            db.AuditLog.Add(new AuditLogEntry
            {
                MspOrganizationId = orgId, Action = action, EntityType = entity,
                ActorDisplayName = actor, CorrelationId = Guid.NewGuid().ToString(), CreatedAt = when,
            });
    }
}
