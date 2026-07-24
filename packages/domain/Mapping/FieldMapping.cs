using Desk.Domain.Common;
using Desk.Domain.Enums;

namespace Desk.Domain.Mapping;

/// <summary>
/// A single field-mapping rule. Rules resolve from most specific scope to least specific
/// (see <see cref="MappingScope"/>). The engine never hard-codes provider terminology —
/// external field/value names are discovered from the connected tenant where the API permits.
/// </summary>
public class FieldMapping : TenantEntity
{
    public ProviderType Provider { get; set; }
    public MappingScope Scope { get; set; } = MappingScope.ProviderDefault;

    /// <summary>Populated when the scope narrows the rule to one connection/company/board/type.</summary>
    public Guid? PsaConnectionId { get; set; }
    public Guid? ClientCompanyId { get; set; }
    public string? QueueOrBoardKey { get; set; }
    public string? TicketTypeKey { get; set; }

    // Portal side
    public required string PortalField { get; set; }
    public string? PortalValue { get; set; }

    // External side
    public required string ExternalField { get; set; }
    public string? ExternalValue { get; set; }

    public MappingDirection Direction { get; set; } = MappingDirection.Bidirectional;
    public bool IsRequired { get; set; }

    /// <summary>Value used when no mapped value resolves.</summary>
    public string? FallbackValue { get; set; }

    /// <summary>Optional JSON predicate for conditional mappings (evaluated by the mapping engine).</summary>
    public string? ConditionJson { get; set; }

    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
}

/// <summary>
/// Immutable snapshot of a mapping set, captured on every change to support preview,
/// version history, and rollback (spec §6).
/// </summary>
public class FieldMappingVersion : TenantEntity
{
    public ProviderType Provider { get; set; }
    public Guid? PsaConnectionId { get; set; }
    public int Version { get; set; }

    /// <summary>Serialized snapshot of all <see cref="FieldMapping"/> rows at this version.</summary>
    public required string SnapshotJson { get; set; }

    public required string ChangedByUserId { get; set; }
    public string? ChangeNote { get; set; }
}
