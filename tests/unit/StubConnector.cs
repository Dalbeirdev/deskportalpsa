using Desk.Domain.Enums;
using Desk.PsaCore.Contracts;
using Desk.PsaCore.Models;

namespace Desk.Tests.Unit;

/// <summary>
/// Minimal hand-driven connector for sync tests: the caller supplies exactly the tickets and notes
/// the provider should return, and every other contract member is deliberately unsupported so a test
/// that reaches beyond what it declared fails loudly instead of silently exercising mock behaviour.
/// </summary>
public sealed class StubConnector(ProviderType provider = ProviderType.AutotaskPsa) : IServiceManagementConnector
{
    public List<UnifiedTicket> Tickets { get; } = [];

    /// <summary>Public notes per external ticket id, as the provider would return them.</summary>
    public Dictionary<string, List<UnifiedTicketNote>> Notes { get; } = [];

    /// <summary>How many times the runner asked for notes — proves per-ticket call behaviour.</summary>
    public int NoteReads { get; private set; }

    /// <summary>When set, note reads fail with it, to prove one bad ticket cannot fail the run.</summary>
    public ConnectorException? NoteReadFailure { get; set; }

    public ProviderType Provider => provider;

    public Task<PaginatedResult<UnifiedTicket>> GetTicketsAsync(TicketFilter filter, CancellationToken ct = default)
        => Task.FromResult(new PaginatedResult<UnifiedTicket>(Tickets, null, false));

    public Task<IReadOnlyList<UnifiedTicketNote>> GetPublicNotesAsync(string ticketId, CancellationToken ct = default)
    {
        NoteReads++;
        if (NoteReadFailure is not null) throw NoteReadFailure;
        return Task.FromResult<IReadOnlyList<UnifiedTicketNote>>(Notes.GetValueOrDefault(ticketId, []));
    }

    private static Task<T> No<T>([System.Runtime.CompilerServices.CallerMemberName] string member = "")
        => throw new NotSupportedException($"{member} is not part of this stub; add it to the test that needs it.");

    public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken ct = default) => No<ProviderCapabilities>();
    public Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default) => No<ConnectionTestResult>();
    public Task<IReadOnlyList<ExternalOrganization>> GetOrganizationsAsync(CancellationToken ct = default) => No<IReadOnlyList<ExternalOrganization>>();
    public Task<IReadOnlyList<ExternalContact>> GetContactsAsync(string organizationId, CancellationToken ct = default) => No<IReadOnlyList<ExternalContact>>();
    public Task<IReadOnlyList<ExternalTechnician>> GetTechniciansAsync(CancellationToken ct = default) => No<IReadOnlyList<ExternalTechnician>>();
    public Task<IReadOnlyList<ExternalDevice>> GetDevicesAsync(string organizationId, CancellationToken ct = default) => No<IReadOnlyList<ExternalDevice>>();
    public Task<UnifiedTicket?> GetTicketAsync(string ticketId, CancellationToken ct = default) => No<UnifiedTicket?>();
    public Task<CreateTicketResult> CreateTicketAsync(UnifiedTicketCreateRequest ticket, CancellationToken ct = default) => No<CreateTicketResult>();
    public Task<UpdateTicketResult> UpdateTicketAsync(string ticketId, UnifiedTicketUpdate update, CancellationToken ct = default) => No<UpdateTicketResult>();
    public Task<CreateNoteResult> AddPublicNoteAsync(string ticketId, UnifiedTicketNoteCreateRequest note, CancellationToken ct = default) => No<CreateNoteResult>();
    public Task<IReadOnlyList<UnifiedAttachment>> GetAttachmentsAsync(string ticketId, CancellationToken ct = default) => No<IReadOnlyList<UnifiedAttachment>>();
    public Task<CreateAttachmentResult> AddAttachmentAsync(string ticketId, SecureAttachment attachment, CancellationToken ct = default) => No<CreateAttachmentResult>();
    public Task<IReadOnlyList<UnifiedTimeEntry>> GetTimeEntriesAsync(string ticketId, CancellationToken ct = default) => No<IReadOnlyList<UnifiedTimeEntry>>();
    public Task<CreateTimeEntryResult> AddTimeEntryAsync(string ticketId, UnifiedTimeEntryCreateRequest entry, CancellationToken ct = default) => No<CreateTimeEntryResult>();
    public Task<UpdateTimeEntryResult> UpdateTimeEntryAsync(string entryId, UnifiedTimeEntryUpdate update, CancellationToken ct = default) => No<UpdateTimeEntryResult>();
    public Task<UpdateTimeEntryResult> DeleteTimeEntryAsync(string entryId, CancellationToken ct = default) => No<UpdateTimeEntryResult>();
    public Task<IReadOnlyList<ExternalFieldOption>> GetStatusesAsync(CancellationToken ct = default) => No<IReadOnlyList<ExternalFieldOption>>();
    public Task<IReadOnlyList<ExternalFieldOption>> GetPrioritiesAsync(CancellationToken ct = default) => No<IReadOnlyList<ExternalFieldOption>>();
    public Task<IReadOnlyList<ExternalFieldOption>> GetQueuesOrBoardsAsync(CancellationToken ct = default) => No<IReadOnlyList<ExternalFieldOption>>();
    public Task<IReadOnlyList<ExternalFieldOption>> GetCategoriesAsync(CancellationToken ct = default) => No<IReadOnlyList<ExternalFieldOption>>();
    public Task<IReadOnlyList<ExternalFieldOption>> GetWorkTypesAsync(CancellationToken ct = default) => No<IReadOnlyList<ExternalFieldOption>>();
    public Task<IReadOnlyList<ExternalFieldOption>> GetWorkRolesAsync(CancellationToken ct = default) => No<IReadOnlyList<ExternalFieldOption>>();
    public Task<IReadOnlyList<ExternalFieldDefinition>> GetCustomFieldsAsync(CancellationToken ct = default) => No<IReadOnlyList<ExternalFieldDefinition>>();
    public Task<WebhookValidationResult> ValidateWebhookAsync(WebhookRequest request, CancellationToken ct = default) => No<WebhookValidationResult>();
    public Task<NormalizedProviderEvent> ProcessWebhookAsync(WebhookRequest request, CancellationToken ct = default) => No<NormalizedProviderEvent>();
}
