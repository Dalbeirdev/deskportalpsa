using Desk.Application.Common;
using Desk.Application.Mapping;
using Desk.Application.Sync;
using Desk.Domain.Enums;
using Desk.Domain.Mapping;
using Desk.Domain.Tenancy;
using Desk.Domain.Tickets;
using Desk.Infrastructure.Persistence;
using Desk.PsaCore.Models;
using Microsoft.EntityFrameworkCore;

namespace Desk.Infrastructure.Sync;

/// <summary>
/// Turns a normalized provider ticket into (or updates) the portal's <see cref="Ticket"/> row.
/// Order of guards: unchanged-hash short-circuit → portal-echo short-circuit → apply. Provider
/// values are translated to portal values with the mapping engine; the raw provider value is kept
/// alongside for traceability.
/// </summary>
public sealed class TicketSyncService(
    DeskDbContext db,
    IMappingEngine mapping,
    ISyncEventStore syncEvents,
    TimeProvider clock) : ITicketSyncService
{
    public async Task<TicketSyncOutcome> UpsertFromProviderAsync(
        Guid psaConnectionId, UnifiedTicket incoming, IReadOnlyList<FieldMapping> rules, CancellationToken ct = default)
    {
        var connection = await db.PsaConnections.FirstOrDefaultAsync(c => c.Id == psaConnectionId, ct)
            ?? throw new NotFoundException("PSA connection");

        var company = await EnsureCompanyAsync(connection, incoming.RequesterExternalId, incoming.CompanyName, ct);
        var ctx = new MappingContext
        {
            Provider = connection.Provider,
            PsaConnectionId = psaConnectionId,
            ClientCompanyId = company.Id,
            QueueOrBoardKey = incoming.QueueOrBoard,
        };

        // Translate provider → portal, passing raw values through when no rule matches.
        var portalStatus = Map(rules, ctx, "status", incoming.Status) ?? incoming.Status ?? "NEW";
        var portalPriority = Map(rules, ctx, "priority", incoming.Priority) ?? incoming.Priority ?? "NORMAL";
        var portalCategory = Map(rules, ctx, "category", incoming.Category) ?? incoming.Category;
        var portalQueue = Map(rules, ctx, "queue", incoming.QueueOrBoard) ?? incoming.QueueOrBoard;

        var hash = UpdateHasher.Compute(new Dictionary<string, string?>
        {
            ["status"] = portalStatus,
            ["priority"] = portalPriority,
            ["category"] = portalCategory,
            ["title"] = incoming.Title,
            ["description"] = incoming.Description,
        });

        var existing = await db.Tickets.FirstOrDefaultAsync(
            t => t.PsaConnectionId == psaConnectionId && t.ExternalTicketId == incoming.ExternalId, ct);

        if (existing is not null)
        {
            if (existing.UpdateHash == hash)
                return TicketSyncOutcome.SkippedUnchanged; // idempotent — nothing changed

            if (await syncEvents.IsPortalEchoAsync(psaConnectionId, existing.Id, hash, ct))
                return TicketSyncOutcome.SkippedEcho;       // our own write coming back
        }

        var ticket = existing ?? new Ticket
        {
            MspOrganizationId = connection.MspOrganizationId,
            PsaConnectionId = psaConnectionId,
            Provider = connection.Provider,
            ExternalTicketId = incoming.ExternalId,
            ClientCompanyId = company.Id,
            CorrelationId = Guid.NewGuid(),
            RequesterName = incoming.RequesterName ?? "Unknown",
            RequesterEmail = incoming.RequesterEmail ?? "unknown@unknown",
            Title = incoming.Title,
        };

        ticket.Title = incoming.Title;
        ticket.Description = incoming.Description;
        ticket.PortalStatus = portalStatus;
        ticket.PsaStatus = incoming.Status;
        ticket.PortalPriority = portalPriority;
        ticket.PsaPriority = incoming.Priority;
        ticket.PortalCategory = portalCategory;
        ticket.PsaCategory = incoming.Category;
        ticket.QueueOrBoard = portalQueue;
        ticket.AssignedTechnicianExternalId = incoming.AssignedTechnicianExternalId;
        ticket.ResolvedAt = incoming.ResolvedAt;
        ticket.UpdateHash = hash;
        ticket.SyncStatus = TicketSyncStatus.Synced;
        ticket.LastSyncedAt = clock.GetUtcNow();
        ticket.Version++;

        if (existing is null)
            db.Tickets.Add(ticket);

        await db.SaveChangesAsync(ct);
        return existing is null ? TicketSyncOutcome.Created : TicketSyncOutcome.Updated;
    }

    private string? Map(IReadOnlyList<FieldMapping> rules, MappingContext ctx, string field, string? value)
    {
        if (value is null) return null;
        var r = mapping.MapToPortal(rules, ctx, field, value);
        return r.Resolved ? r.Value : null;
    }

    /// <summary>Company sync: find the client company for this external id, creating a stub if new.</summary>
    private async Task<ClientCompany> EnsureCompanyAsync(PsaConnection connection, string? externalCompanyId, string? companyName, CancellationToken ct)
    {
        var extId = string.IsNullOrEmpty(externalCompanyId) ? "unknown" : externalCompanyId;
        var company = await db.ClientCompanies.FirstOrDefaultAsync(
            c => c.PsaConnectionId == connection.Id && c.ExternalCompanyId == extId, ct);
        if (company is not null)
        {
            // Upgrade a placeholder (or stale) name when the provider sends the real one inline.
            if (!string.IsNullOrWhiteSpace(companyName) && company.Name != companyName)
            {
                company.Name = companyName;
                await db.SaveChangesAsync(ct);
            }
            return company;
        }

        company = new ClientCompany
        {
            MspOrganizationId = connection.MspOrganizationId,
            PsaConnectionId = connection.Id,
            ExternalCompanyId = extId,
            // Prefer the provider-supplied name; placeholder until a company sync fills it otherwise.
            Name = string.IsNullOrWhiteSpace(companyName) ? $"Company {extId}" : companyName,
        };
        db.ClientCompanies.Add(company);
        await db.SaveChangesAsync(ct);
        return company;
    }
}
