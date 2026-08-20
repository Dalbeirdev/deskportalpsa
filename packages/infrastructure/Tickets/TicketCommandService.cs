using Desk.Application.Common;
using Desk.Application.Connectors;
using Desk.Application.Mapping;
using Desk.Application.Sync;
using Desk.Application.Tickets;
using Desk.Domain.Authorization;
using Desk.Domain.Enums;
using Desk.Domain.Mapping;
using Desk.Domain.Tickets;
using Desk.Infrastructure.Persistence;
using Desk.PsaCore.Contracts;
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
    ITicketScopeQuery scopeQuery,
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
        CreateTicketResult created;
        try
        {
            created = await connector.CreateTicketAsync(new UnifiedTicketCreateRequest
        {
            Title = input.Title,
            Description = input.Description,
            // The portal row starts at NEW, so tell the provider the same thing. Some PSAs
            // (Autotask) reject a create without a status, and leaving it unset also meant the
            // provider chose its own default — diverging from the portal on the very first write.
            Status = MapOut(rules, ctx, "status", NewStatus),
            Priority = MapOut(rules, ctx, "priority", input.Priority),
            Category = MapOut(rules, ctx, "category", input.Category),
            // Queue: use the caller's choice when given, else the connection's configured default
            // (already a provider id, so it bypasses mapping). Providers such as Autotask require one.
            QueueOrBoard = input.QueueOrBoard is not null
                ? MapOut(rules, ctx, "queue", input.QueueOrBoard)
                : connection.DefaultQueueOrBoardId,
            TicketType = connection.DefaultTicketType,
            IssueType = connection.DefaultIssueType,
            SubIssueType = connection.DefaultSubIssueType,
            ExternalCompanyId = company.ExternalCompanyId,
            RequesterExternalId = requester.ExternalContactId,
            RequesterEmail = requester.Email,
            IdempotencyKey = idempotencyKey,
            }, ct);
        }
        catch (ConnectorException ex)
        {
            // An unreachable or rate-limited PSA is not the customer's problem to retype.
            created = new CreateTicketResult(false, null, ex.Message);
        }

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
            // Recorded either way. A rejected create used to throw before anything was written, so
            // the customer's ticket vanished with nothing to retry from and no count of what was
            // lost. It is kept here as Error, listed for staff, and resyncable.
            SyncStatus = created.Success ? TicketSyncStatus.Synced : TicketSyncStatus.Error,
            SyncError = created.Success ? null : created.Error,
            LastSyncedAt = created.Success ? now : null,
            CorrelationId = Guid.NewGuid(),
        };
        ticket.UpdateHash = HashOf(ticket);
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(ct);

        if (!created.Success)
        {
            // Still surfaced as a failure: the customer must not be told it reached the PSA when it
            // did not. The row exists so the work can be recovered rather than retyped.
            await RecordPortalEventAsync(access.MspOrganizationId, connection.Id, ticket, idempotencyKey, "ticket.create_failed", ct);
            throw new ValidationFailedException(created.Error ?? "The PSA rejected the ticket.");
        }

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

    /// <summary>
    /// A technician reply from the staff dashboard. Same provider push and echo bookkeeping as a
    /// client comment; only the attribution differs — the staff display name, never a client flag.
    /// isPublic=false posts an INTERNAL note: pushed to the PSA flagged internal (ConnectWise
    /// internalAnalysisFlag / Autotask internal publish value) and stored IsPublic=false, so the
    /// client read path never returns it. Only the staff endpoint can reach this parameter.
    /// </summary>
    public async Task<TicketNoteDto> AddStaffCommentAsync(Guid appUserId, string authorName, Guid ticketId, string body, bool isPublic = true, CancellationToken ct = default)
    {
        var ticket = await scopeQuery.FindAsync(db.Tickets, ticketId, appUserId, Permissions.TicketsAddPublicNote, ct)
            ?? throw new NotFoundException("Ticket");
        if (string.IsNullOrEmpty(ticket.ExternalTicketId))
            throw new ValidationFailedException("This ticket is not yet synced to the PSA, so a reply cannot be posted.");

        var idempotencyKey = Guid.NewGuid().ToString("N");
        var connector = await connectors.ResolveAsync(ticket.PsaConnectionId, ct);
        var result = await connector.AddPublicNoteAsync(
            ticket.ExternalTicketId, new UnifiedTicketNoteCreateRequest(body, IsPublic: isPublic, idempotencyKey), ct);
        if (!result.Success)
            throw new ValidationFailedException(result.Error ?? "The PSA rejected the comment.");

        var note = new TicketNote
        {
            MspOrganizationId = ticket.MspOrganizationId,
            TicketId = ticket.Id,
            ExternalNoteId = result.ExternalId,
            AuthorName = authorName,
            AuthoredByClient = false,
            Body = body,
            IsPublic = isPublic,
            NoteCreatedAt = clock.GetUtcNow(),
            OriginCorrelationId = ticket.CorrelationId,
        };
        db.TicketNotes.Add(note);
        await db.SaveChangesAsync(ct);

        await RecordPortalEventAsync(ticket.MspOrganizationId, ticket.PsaConnectionId, ticket, idempotencyKey, "note.created", ct);
        return new TicketNoteDto(note.Id, note.AuthorName, false, note.Body, note.NoteCreatedAt);
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
