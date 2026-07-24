using Desk.Domain.Common;

namespace Desk.Domain.Audit;

/// <summary>
/// Append-only audit record. Never updated or deleted in normal operation (spec §11:
/// immutable audit trail). Persistence layer forbids modifying existing rows.
/// </summary>
public class AuditLogEntry : BaseEntity
{
    /// <summary>Null only for platform-level actions not tied to one tenant.</summary>
    public Guid? MspOrganizationId { get; set; }

    public required string Action { get; set; }
    public required string EntityType { get; set; }
    public string? EntityId { get; set; }

    public string? ActorUserId { get; set; }
    public string? ActorDisplayName { get; set; }
    public string? ActorIpAddress { get; set; }

    /// <summary>Correlation id linking this entry to the originating request/sync chain.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Redacted JSON detail — secrets are masked before write.</summary>
    public string? DetailJson { get; set; }
}
