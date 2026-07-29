using Desk.Domain.Enums;

namespace Desk.Application.Mapping;

/// <summary>
/// The situational context a mapping is resolved against. More specific fields (client, queue,
/// ticket type) let narrower-scoped rules win over provider/platform defaults.
/// </summary>
public sealed record MappingContext
{
    public required ProviderType Provider { get; init; }
    public required Guid PsaConnectionId { get; init; }
    public Guid? ClientCompanyId { get; init; }
    public string? QueueOrBoardKey { get; init; }
    public string? TicketTypeKey { get; init; }
}

/// <summary>Outcome of resolving a single field mapping.</summary>
public sealed record MappingResult
{
    public bool Resolved { get; init; }
    public string? Value { get; init; }
    public Guid? MatchedRuleId { get; init; }
    public MappingScope? MatchedScope { get; init; }
    public bool UsedFallback { get; init; }
    public string? Error { get; init; }

    public static MappingResult Hit(string? value, Guid ruleId, MappingScope scope, bool fallback = false)
        => new() { Resolved = true, Value = value, MatchedRuleId = ruleId, MatchedScope = scope, UsedFallback = fallback };

    public static MappingResult Miss(string? error = null)
        => new() { Resolved = false, Error = error };
}
