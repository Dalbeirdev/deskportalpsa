using Desk.Application.Mapping;
using Desk.Domain.Enums;
using Desk.Domain.Mapping;
using FluentAssertions;
using Xunit;

namespace Desk.Tests.Unit;

public class MappingEngineTests
{
    private static readonly Guid Conn = Guid.NewGuid();
    private static readonly Guid Client = Guid.NewGuid();
    private readonly MappingEngine _engine = new();

    private static MappingContext Ctx() => new()
    {
        Provider = ProviderType.ConnectWisePsa,
        PsaConnectionId = Conn,
        ClientCompanyId = Client,
        QueueOrBoardKey = "Service Desk",
    };

    private static FieldMapping Rule(MappingScope scope, string portalVal, string extVal, MappingDirection dir = MappingDirection.Bidirectional)
        => new()
        {
            MspOrganizationId = Guid.NewGuid(),
            Provider = ProviderType.ConnectWisePsa,
            Scope = scope,
            PsaConnectionId = Conn,
            ClientCompanyId = Client,
            QueueOrBoardKey = "Service Desk",
            PortalField = "status",
            ExternalField = "status",
            PortalValue = portalVal,
            ExternalValue = extVal,
            Direction = dir,
        };

    [Fact]
    public void Provider_default_maps_portal_value_to_external()
    {
        var rules = new[] { Rule(MappingScope.ProviderDefault, "IN_PROGRESS", "In Progress") };
        var r = _engine.MapToProvider(rules, Ctx(), "status", "IN_PROGRESS");
        r.Resolved.Should().BeTrue();
        r.Value.Should().Be("In Progress");
    }

    [Fact]
    public void More_specific_scope_wins_over_default()
    {
        var rules = new[]
        {
            Rule(MappingScope.ProviderDefault, "IN_PROGRESS", "In Progress"),
            Rule(MappingScope.ConnectionOverride, "IN_PROGRESS", "Working"),
        };
        var r = _engine.MapToProvider(rules, Ctx(), "status", "IN_PROGRESS");
        r.Value.Should().Be("Working");
        r.MatchedScope.Should().Be(MappingScope.ConnectionOverride);
    }

    [Fact]
    public void Client_override_beats_connection_override()
    {
        var rules = new[]
        {
            Rule(MappingScope.ConnectionOverride, "IN_PROGRESS", "Working"),
            Rule(MappingScope.ClientCompanyOverride, "IN_PROGRESS", "Actioned"),
        };
        var r = _engine.MapToProvider(rules, Ctx(), "status", "IN_PROGRESS");
        r.Value.Should().Be("Actioned");
    }

    [Fact]
    public void Inbound_maps_external_value_back_to_portal()
    {
        var rules = new[] { Rule(MappingScope.ProviderDefault, "IN_PROGRESS", "In Progress") };
        var r = _engine.MapToPortal(rules, Ctx(), "status", "In Progress");
        r.Value.Should().Be("IN_PROGRESS");
    }

    [Fact]
    public void Read_only_rule_is_never_written_outbound()
    {
        var rules = new[] { Rule(MappingScope.ProviderDefault, "IN_PROGRESS", "In Progress", MappingDirection.ReadOnly) };
        var r = _engine.MapToProvider(rules, Ctx(), "status", "IN_PROGRESS");
        r.Resolved.Should().BeFalse();
    }

    [Fact]
    public void One_way_provider_to_portal_is_not_used_outbound()
    {
        var rules = new[] { Rule(MappingScope.ProviderDefault, "IN_PROGRESS", "In Progress", MappingDirection.ProviderToPortal) };
        _engine.MapToProvider(rules, Ctx(), "status", "IN_PROGRESS").Resolved.Should().BeFalse();
        _engine.MapToPortal(rules, Ctx(), "status", "In Progress").Resolved.Should().BeTrue();
    }

    [Fact]
    public void Required_field_with_no_match_and_no_fallback_is_a_miss()
    {
        var rule = new FieldMapping
        {
            MspOrganizationId = Guid.NewGuid(), Provider = ProviderType.ConnectWisePsa,
            Scope = MappingScope.ProviderDefault, PortalField = "priority", ExternalField = "priority",
            PortalValue = "URGENT", ExternalValue = null, IsRequired = true,
        };
        var r = _engine.MapToProvider(new[] { rule }, Ctx(), "priority", "URGENT");
        r.Resolved.Should().BeFalse();
        r.Error.Should().Contain("Required");
    }

    [Fact]
    public void Falls_back_when_value_unmapped_and_fallback_present()
    {
        var rule = new FieldMapping
        {
            MspOrganizationId = Guid.NewGuid(), Provider = ProviderType.ConnectWisePsa,
            Scope = MappingScope.ProviderDefault, PortalField = "priority", ExternalField = "priority",
            PortalValue = "URGENT", ExternalValue = null, FallbackValue = "High",
        };
        var r = _engine.MapToProvider(new[] { rule }, Ctx(), "priority", "URGENT");
        r.Resolved.Should().BeTrue();
        r.Value.Should().Be("High");
        r.UsedFallback.Should().BeTrue();
    }

    [Fact]
    public void Connection_scoped_rule_for_other_connection_does_not_apply()
    {
        var rule = Rule(MappingScope.ConnectionOverride, "IN_PROGRESS", "Working");
        rule.PsaConnectionId = Guid.NewGuid(); // different connection
        var r = _engine.MapToProvider(new[] { rule }, Ctx(), "status", "IN_PROGRESS");
        r.Resolved.Should().BeFalse();
    }
}
