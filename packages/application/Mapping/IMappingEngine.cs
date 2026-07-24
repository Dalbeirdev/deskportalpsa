using Desk.Domain.Mapping;

namespace Desk.Application.Mapping;

/// <summary>
/// Resolves portal ⇄ provider field values against a set of mapping rules. The engine is pure:
/// callers load the candidate <see cref="FieldMapping"/> rows (tenant-scoped) and pass them in,
/// which keeps resolution deterministic and unit-testable with no I/O.
/// </summary>
public interface IMappingEngine
{
    /// <summary>Portal → provider (outbound writes). Skips read-only rules.</summary>
    MappingResult MapToProvider(IReadOnlyList<FieldMapping> rules, MappingContext ctx, string portalField, string? portalValue);

    /// <summary>Provider → portal (inbound reads). Skips write-only rules.</summary>
    MappingResult MapToPortal(IReadOnlyList<FieldMapping> rules, MappingContext ctx, string externalField, string? externalValue);
}
