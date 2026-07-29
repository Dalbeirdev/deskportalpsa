using Desk.Domain.Enums;
using Desk.PsaCore.Models;

namespace Desk.PsaCore.Contracts;

/// <summary>
/// The single contract every PSA / service-desk connector implements. Provider-specific
/// code lives ONLY behind this interface — controllers, UI, and core services stay
/// provider-neutral (spec §3, Integration Plan connector contract).
///
/// A connector instance is bound to one <see cref="Desk.Domain.Tenancy.PsaConnection"/> and
/// resolves its credentials from the secret store; it never receives raw secrets from callers.
/// </summary>
public interface IServiceManagementConnector
{
    ProviderType Provider { get; }

    Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken ct = default);
    Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default);

    // Directory
    Task<IReadOnlyList<ExternalOrganization>> GetOrganizationsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ExternalContact>> GetContactsAsync(string organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<ExternalTechnician>> GetTechniciansAsync(CancellationToken ct = default);

    // Tickets
    Task<PaginatedResult<UnifiedTicket>> GetTicketsAsync(TicketFilter filter, CancellationToken ct = default);
    Task<UnifiedTicket?> GetTicketAsync(string ticketId, CancellationToken ct = default);
    Task<CreateTicketResult> CreateTicketAsync(UnifiedTicketCreateRequest ticket, CancellationToken ct = default);
    Task<UpdateTicketResult> UpdateTicketAsync(string ticketId, UnifiedTicketUpdate update, CancellationToken ct = default);

    // Notes (public/private separation enforced by callers using capabilities)
    Task<IReadOnlyList<UnifiedTicketNote>> GetPublicNotesAsync(string ticketId, CancellationToken ct = default);
    Task<CreateNoteResult> AddPublicNoteAsync(string ticketId, UnifiedTicketNoteCreateRequest note, CancellationToken ct = default);

    // Attachments
    Task<IReadOnlyList<UnifiedAttachment>> GetAttachmentsAsync(string ticketId, CancellationToken ct = default);
    Task<CreateAttachmentResult> AddAttachmentAsync(string ticketId, SecureAttachment attachment, CancellationToken ct = default);

    // Time
    Task<IReadOnlyList<UnifiedTimeEntry>> GetTimeEntriesAsync(string ticketId, CancellationToken ct = default);
    Task<CreateTimeEntryResult> AddTimeEntryAsync(string ticketId, UnifiedTimeEntryCreateRequest entry, CancellationToken ct = default);
    Task<UpdateTimeEntryResult> UpdateTimeEntryAsync(string entryId, UnifiedTimeEntryUpdate update, CancellationToken ct = default);
    Task<UpdateTimeEntryResult> DeleteTimeEntryAsync(string entryId, CancellationToken ct = default);

    // Field discovery (live from the connected tenant — never hard-coded)
    Task<IReadOnlyList<ExternalFieldOption>> GetStatusesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ExternalFieldOption>> GetPrioritiesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ExternalFieldOption>> GetQueuesOrBoardsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ExternalFieldOption>> GetCategoriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ExternalFieldOption>> GetWorkTypesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ExternalFieldOption>> GetWorkRolesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ExternalFieldDefinition>> GetCustomFieldsAsync(CancellationToken ct = default);

    // Webhooks
    Task<WebhookValidationResult> ValidateWebhookAsync(WebhookRequest request, CancellationToken ct = default);
    Task<NormalizedProviderEvent> ProcessWebhookAsync(WebhookRequest request, CancellationToken ct = default);
}

/// <summary>
/// Resolves a connector instance for a given PSA connection. Registered per provider;
/// adding a provider means registering a new factory — no core code changes.
/// </summary>
public interface IConnectorFactory
{
    ProviderType Provider { get; }

    /// <summary>Build a connector bound to the given connection id (credentials resolved internally).</summary>
    Task<IServiceManagementConnector> CreateAsync(Guid psaConnectionId, CancellationToken ct = default);
}
