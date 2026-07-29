using Desk.Application.Common;
using Desk.Application.Connectors;
using Desk.Domain.Enums;
using Desk.Infrastructure.Persistence;
using Desk.PsaCore.Contracts;
using Desk.PsaCore.Models;
using Microsoft.EntityFrameworkCore;

namespace Desk.Infrastructure.Connectors;

/// <summary>
/// Local-mode connector: satisfies the connector contract entirely in-process so the platform's
/// write paths (create ticket, add note, attach) and live field discovery work with no external PSA
/// and no stored credentials. Registered ONLY in local mode, in place of the real per-provider
/// factories. Writes report success with a synthetic external id; reads return the curated demo
/// option sets. Never used outside the no-Docker demo.
/// </summary>
public sealed class LocalStubConnector(ProviderType provider) : IServiceManagementConnector
{
    public ProviderType Provider => provider;

    public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken ct = default)
        => Task.FromResult(new ProviderCapabilities
        {
            SupportsTicketCreate = true, SupportsTicketUpdate = true, SupportsPublicNotes = true,
            SupportsAttachments = true, SupportsTimeEntries = true, SupportsCompanies = true,
            SupportsContacts = true, SupportsTechnicians = true, SupportsQueues = true,
            SupportsCustomFields = false, SupportsInboundWebhooks = true, SupportsIncrementalSync = true,
            MaximumPageSize = 100, MaximumAttachmentSize = 25 * 1024 * 1024,
            RateLimitModel = "local", AuthenticationTypes = new[] { AuthenticationType.ApiKey },
        });

    public Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default)
        => Task.FromResult(new ConnectionTestResult(true, "Local demo connector — no external PSA.", TimeSpan.FromMilliseconds(4)));

    // Directory
    public Task<IReadOnlyList<ExternalOrganization>> GetOrganizationsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ExternalOrganization>>([]);
    public Task<IReadOnlyList<ExternalContact>> GetContactsAsync(string organizationId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ExternalContact>>([]);
    public Task<IReadOnlyList<ExternalTechnician>> GetTechniciansAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ExternalTechnician>>([]);

    // Tickets
    public Task<PaginatedResult<UnifiedTicket>> GetTicketsAsync(TicketFilter filter, CancellationToken ct = default)
        => Task.FromResult(new PaginatedResult<UnifiedTicket>([], null, false));
    public Task<UnifiedTicket?> GetTicketAsync(string ticketId, CancellationToken ct = default)
        => Task.FromResult<UnifiedTicket?>(null);
    public Task<CreateTicketResult> CreateTicketAsync(UnifiedTicketCreateRequest ticket, CancellationToken ct = default)
        => Task.FromResult(new CreateTicketResult(true, "LOCAL-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), null));
    public Task<UpdateTicketResult> UpdateTicketAsync(string ticketId, UnifiedTicketUpdate update, CancellationToken ct = default)
        => Task.FromResult(new UpdateTicketResult(true, null));

    // Notes
    public Task<IReadOnlyList<UnifiedTicketNote>> GetPublicNotesAsync(string ticketId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<UnifiedTicketNote>>([]);
    public Task<CreateNoteResult> AddPublicNoteAsync(string ticketId, UnifiedTicketNoteCreateRequest note, CancellationToken ct = default)
        => Task.FromResult(new CreateNoteResult(true, "LOCALNOTE-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), null));

    // Attachments
    public Task<IReadOnlyList<UnifiedAttachment>> GetAttachmentsAsync(string ticketId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<UnifiedAttachment>>([]);
    public Task<CreateAttachmentResult> AddAttachmentAsync(string ticketId, SecureAttachment attachment, CancellationToken ct = default)
        => Task.FromResult(new CreateAttachmentResult(true, "LOCALATT-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), null));

    // Time
    public Task<IReadOnlyList<UnifiedTimeEntry>> GetTimeEntriesAsync(string ticketId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<UnifiedTimeEntry>>([]);

    // Field discovery — the curated demo option sets, mirroring the seeded mappings.
    public Task<IReadOnlyList<ExternalFieldOption>> GetStatusesAsync(CancellationToken ct = default)
        => Opts("New (Not Responded)", "In Progress", "Waiting on Customer", "On Hold", "Resolved", "Closed", "Scheduled", "Escalated");
    public Task<IReadOnlyList<ExternalFieldOption>> GetPrioritiesAsync(CancellationToken ct = default)
        => Opts("Priority 1 - Emergency", "Priority 2 - High", "Priority 3 - Medium", "Priority 4 - Low", "No SLA");
    public Task<IReadOnlyList<ExternalFieldOption>> GetQueuesOrBoardsAsync(CancellationToken ct = default)
        => Opts("Service Desk", "Network Operations", "Professional Services", "Triage", "Onboarding");
    public Task<IReadOnlyList<ExternalFieldOption>> GetCategoriesAsync(CancellationToken ct = default)
        => Opts("Hardware", "Software", "Network", "Account / Access", "Email", "Security");
    public Task<IReadOnlyList<ExternalFieldDefinition>> GetCustomFieldsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ExternalFieldDefinition>>([]);

    // Webhooks
    public Task<WebhookValidationResult> ValidateWebhookAsync(WebhookRequest request, CancellationToken ct = default)
        => Task.FromResult(new WebhookValidationResult(true, null));
    public Task<NormalizedProviderEvent> ProcessWebhookAsync(WebhookRequest request, CancellationToken ct = default)
        => Task.FromResult(new NormalizedProviderEvent("noop", null, Guid.NewGuid().ToString("N"), request.ReceivedAt));

    private static Task<IReadOnlyList<ExternalFieldOption>> Opts(params string[] labels)
        => Task.FromResult<IReadOnlyList<ExternalFieldOption>>(
            labels.Select(l => new ExternalFieldOption(l, l)).ToList());
}

/// <summary>
/// Local-mode connector resolver: returns a <see cref="LocalStubConnector"/> for any enabled
/// connection, bypassing the secret store and real provider factories. Registered only in local mode.
/// </summary>
public sealed class LocalConnectorResolver(DeskDbContext db) : IConnectorResolver
{
    public async Task<IServiceManagementConnector> ResolveAsync(Guid psaConnectionId, CancellationToken ct = default)
    {
        var connection = await db.PsaConnections.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == psaConnectionId, ct)
            ?? throw new NotFoundException("PSA connection");
        if (!connection.IsEnabled)
            throw new ValidationFailedException($"PSA connection '{connection.Name}' is disabled.");
        return new LocalStubConnector(connection.Provider);
    }
}
