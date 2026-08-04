using Desk.Application.Common;
using Desk.Application.Connectors;
using Desk.Application.Mapping;
using Desk.Application.Sync;
using Desk.Application.Tickets;
using Desk.Domain.Enums;
using Desk.Domain.Mapping;
using Desk.Domain.Tickets;
using Desk.Infrastructure.Persistence;
using Desk.PsaCore.Models;
using Microsoft.EntityFrameworkCore;

namespace Desk.Infrastructure.Tickets;

/// <summary>
/// Client-portal writes. The PSA is the system of record, so create/comment go to the provider
/// first; only on success is the portal projection written. Each write records a portal-origin
/// sync event (with the resulting state hash) so the inbound sync recognises and skips its own echo.
/// </summary>
public sealed class TicketCommandService(
    DeskDbContext db,
    IConnectorResolver connectors,
    IMappingEngine mapping,
    ISyncEventStore syncEvents,
    TimeProvider clock) : ITicketCommandService
{
    /// <summary>Portal-neutral status a newly raised ticket starts in.</summary>
    private const string NewStatus = "NEW";

    public async Task<CreateTicketResultDto> CreateAsync(ClientAccess access, CreateTicketInput input, CancellationToken ct = default)
    {
        var company = await db.ClientCompanies.FirstOrDefaultAsync(c => c.Id == access.ClientCompanyId, ct)
            ?? throw new NotFoundException("Client company");
        var connection = await db.PsaConnections.FirstOrDefaultAsync(c => c.Id == company.PsaConnectionId, ct)
            ?? throw new NotFoundException("PSA connection");
        var requester = await db.ClientUsers.FirstOrDefaultAsync(u => u.Id == access.ClientUserId, ct)
            ?? throw new NotFoundException("Client user");

        var rules = await LoadRulesAsync(access.MspOrganizationId, connection.Provider, ct);
        var ctx = new MappingContext
        {
            Provider = connection.Provider, PsaConnectionId = connection.Id,
            ClientCompanyId = company.Id, QueueOrBoardKey = input.QueueOrBoard,
        };

        var idempotencyKey = Guid.NewGuid().ToString("N");
        var connector = await connectors.ResolveAsync(connection.Id, ct);
        var created = await connector.CreateTicketAsync(new UnifiedTicketCreateRequest
        {
            Title = input.Title,
            Description = input.Description,
            // The portal row starts at NEW, so tell the provider the same thing. Some PSAs
            // (Autotask) reject a create without a status, and leaving it unset also meant the
            // provider chose its own default — diverging from the portal on the very first write.
            Status = MapOut(rules, ctx, "status", NewStatus),
            Priority = MapOut(rules, ctx, "priority", input.Priority),
            Category = MapOut(rules, ctx, "category", input.Category),
            QueueOrBoard = MapOut(rules, ctx, "queue", input.QueueOrBoard),
            ExternalCompanyId = company.ExternalCompanyId,
            RequesterExternalId = requester.ExternalContactId,
            RequesterEmail = requester.Email,
            IdempotencyKey = idempotencyKey,
        }, ct);

        if (!created.Success)
            throw new ValidationFailedException(created.Error ?? "The PSA rejected the ticket.");

        var now = clock.GetUtcNow();
        var ticket = new Ticket
        {
            MspOrganizationId = access.MspOrganizationId,
            PsaConnectionId = connection.Id,
            Provider = connection.Provider,
            ExternalTicketId = created.ExternalId,
            ClientCompanyId = company.Id,
            RequesterUserId = requester.Id,
            RequesterName = requester.DisplayName,
            RequesterEmail = requester.Email,
            Title = input.Title,
            Description = input.Description,
            PortalStatus = NewStatus,
            PortalPriority = input.Priority ?? "NORMAL",
            PortalCategory = input.Category,
            QueueOrBoard = input.QueueOrBoard,
            SyncStatus = TicketSyncStatus.Synced,
            LastSyncedAt = now,
            CorrelationId = Guid.NewGuid(),
        };
        ticket.UpdateHash = HashOf(ticket);
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(ct);

        await RecordPortalEventAsync(access.MspOrganizationId, connection.Id, ticket, idempotencyKey, "ticket.created", ct);
        return new CreateTicketResultDto(ticket.Id, created.ExternalId);
    }

    public async Task<TicketNoteDto> AddCommentAsync(ClientAccess access, Guid ticketId, string body, CancellationToken ct = default)
    {
        var ticket = await db.Tickets.FirstOrDefaultAsync(t =>
            t.Id == ticketId
            && t.ClientCompanyId == access.ClientCompanyId
            && (access.IsCompanyAdministrator || t.RequesterUserId == access.ClientUserId), ct)
            ?? throw new NotFoundException("Ticket");
        var requester = await db.ClientUsers.FirstOrDefaultAsync(u => u.Id == access.ClientUserId, ct)
            ?? throw new NotFoundException("Client user");

        var idempotencyKey = Guid.NewGuid().ToString("N");
        var connector = await connectors.ResolveAsync(ticket.PsaConnectionId, ct);
        var result = await connector.AddPublicNoteAsync(
            ticket.ExternalTicketId!, new UnifiedTicketNoteCreateRequest(body, IsPublic: true, idempotencyKey), ct);
        if (!result.Success)
            throw new ValidationFailedException(result.Error ?? "The PSA rejected the comment.");

        var now = clock.GetUtcNow();
        var note = new TicketNote
        {
            MspOrganizationId = access.MspOrganizationId,
            TicketId = ticket.Id,
            ExternalNoteId = result.ExternalId,
            AuthorName = requester.DisplayName,
            AuthoredByClient = true,
            Body = body,
            IsPublic = true,
            NoteCreatedAt = now,
            OriginCorrelationId = ticket.CorrelationId,
        };
        db.TicketNotes.Add(note);
        await db.SaveChangesAsync(ct);

        await RecordPortalEventAsync(access.MspOrganizationId, ticket.PsaConnectionId, ticket, idempotencyKey, "note.created", ct);
        return new TicketNoteDto(note.Id, note.AuthorName, true, note.Body, note.NoteCreatedAt);
    }

    private async Task<List<FieldMapping>> LoadRulesAsync(Guid org, ProviderType provider, CancellationToken ct)
        => await db.FieldMappings.AsNoTracking()
            .Where(m => m.Provider == provider && m.IsActive)
            .ToListAsync(ct);

    private string? MapOut(List<FieldMapping> rules, MappingContext ctx, string field, string? value)
    {
        if (value is null) return null;
        var r = mapping.MapToProvider(rules, ctx, field, value);
        return r.Resolved ? r.Value : value; // passthrough when unmapped
    }

    private static string HashOf(Ticket t) => UpdateHasher.Compute(new Dictionary<string, string?>
    {
        ["status"] = t.PortalStatus, ["priority"] = t.PortalPriority, ["category"] = t.PortalCategory,
        ["title"] = t.Title, ["description"] = t.Description,
    });

    private Task RecordPortalEventAsync(Guid org, Guid connId, Ticket ticket, string idemKey, string eventType, CancellationToken ct)
        => syncEvents.TryRegisterAsync(new SyncEventRegistration
        {
            MspOrganizationId = org,
            PsaConnectionId = connId,
            TicketId = ticket.Id,
            EventType = eventType,
            IdempotencyKey = idemKey,
            SourceMarker = SyncSource.Portal,
            CorrelationId = ticket.CorrelationId,
            PayloadHash = ticket.UpdateHash,
            OccurredAt = clock.GetUtcNow(),
        }, ct);
}
