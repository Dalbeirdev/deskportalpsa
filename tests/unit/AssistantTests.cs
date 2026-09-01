using Desk.Application.Abstractions;
using Desk.Application.Assistant;
using Desk.Application.Common;
using Desk.Domain.Assistant;
using Desk.Domain.Enums;
using Desk.Domain.Tenancy;
using Desk.Domain.Tickets;
using Desk.Infrastructure.Assistant;
using FluentAssertions;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// The assistant's boundaries, which are the part worth pinning: what leaves the building, what it
/// refuses to do, and whether it is switched on at all. The model's prose is not under test — the
/// fake records the prompt so the CONTEXT can be asserted exactly.
/// </summary>
public class AssistantTests
{
    private static readonly Guid Org = Guid.NewGuid();

    private sealed class RecordingModel : IAssistantModel
    {
        public string? Prompt { get; private set; }
        public string? Key { get; private set; }
        public Task<string> CompleteAsync(string apiKey, string model, string systemPrompt, string userPrompt, CancellationToken ct = default)
        {
            Key = apiKey; Prompt = userPrompt;
            return Task.FromResult("answer");
        }
    }

    private sealed class FakeSecrets : ISecretStore
    {
        private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _s = [];
        public Task<string> WriteAsync(string logicalName, IReadOnlyDictionary<string, string> data, CancellationToken ct = default)
        { var r = $"mem://{Guid.NewGuid():N}"; _s[r] = data; return Task.FromResult(r); }
        public Task<IReadOnlyDictionary<string, string>> ReadAsync(string secretRef, CancellationToken ct = default)
            => Task.FromResult(_s.TryGetValue(secretRef, out var d) ? d : throw new KeyNotFoundException(secretRef));
        public Task<string> RotateAsync(string secretRef, IReadOnlyDictionary<string, string> data, CancellationToken ct = default)
        { _s[secretRef] = data; return Task.FromResult(secretRef); }
        public Task DeleteAsync(string secretRef, CancellationToken ct = default) { _s.Remove(secretRef); return Task.CompletedTask; }
    }

    private static async Task<(AssistantService svc, RecordingModel model, AdminHarness h, Guid ticketId)>
        BuildAsync(bool enabled = true, bool withKey = true, bool includeInternal = false)
    {
        var h = AdminHarness.Create(Org);
        var secrets = new FakeSecrets();
        var conn = Guid.NewGuid();
        h.Db.PsaConnections.Add(new PsaConnection
        {
            Id = conn, MspOrganizationId = Org, Name = "AT", Provider = ProviderType.AutotaskPsa,
            ApiEndpoint = "https://x", CredentialSecretRef = "mem://x",
        });
        var company = new ClientCompany { MspOrganizationId = Org, PsaConnectionId = conn, Name = "Acme", ExternalCompanyId = "1" };
        h.Db.ClientCompanies.Add(company);
        var ticket = new Ticket
        {
            MspOrganizationId = Org, ClientCompanyId = company.Id, PsaConnectionId = conn,
            Title = "Outlook will not connect", RequesterName = "r", RequesterEmail = "r@a.test",
            PortalStatus = "WAITING_CUSTOMER", PortalPriority = "MEDIUM",
        };
        h.Db.Tickets.Add(ticket);
        h.Db.TicketNotes.Add(new TicketNote
        {
            MspOrganizationId = Org, TicketId = ticket.Id, AuthorName = "Komal",
            AuthoredByClient = true, Body = "PUBLIC-CUSTOMER-TEXT", IsPublic = true, NoteCreatedAt = h.Clock.GetUtcNow(),
        });
        h.Db.TicketNotes.Add(new TicketNote
        {
            MspOrganizationId = Org, TicketId = ticket.Id, AuthorName = "Harpal",
            AuthoredByClient = false, Body = "SECRET-INTERNAL-TEXT", IsPublic = false, NoteCreatedAt = h.Clock.GetUtcNow().AddMinutes(1),
        });

        var settings = new AssistantSettings { MspOrganizationId = Org, IsEnabled = enabled, IncludeInternalNotes = includeInternal };
        if (withKey)
            settings.CredentialSecretRef = await secrets.WriteAsync("assistant", new Dictionary<string, string> { ["ApiKey"] = "KEY-123" });
        h.Db.AssistantSettings.Add(settings);
        await h.Db.SaveChangesAsync();

        var model = new RecordingModel();
        return (new AssistantService(h.Db, secrets, model, h.Tenant), model, h, ticket.Id);
    }

    [Fact]
    public async Task Internal_notes_do_not_leave_the_building_unless_the_tenant_opted_in()
    {
        var (svc, model, _, id) = await BuildAsync();

        await svc.AskAsync(id, AssistantAction.Summarise, null);

        model.Prompt.Should().Contain("PUBLIC-CUSTOMER-TEXT");
        model.Prompt.Should().NotContain("SECRET-INTERNAL-TEXT",
            "internal notes carry private commentary and must not reach a third party by default");
        model.Prompt.Should().Contain("internal notes exist",
            "a summary that silently omits half the thread is worse than one that says what it read");

        // Opted in, they travel — the setting has to actually do something.
        var (svc2, model2, _, id2) = await BuildAsync(includeInternal: true);
        await svc2.AskAsync(id2, AssistantAction.Summarise, null);
        model2.Prompt.Should().Contain("SECRET-INTERNAL-TEXT");
    }

    [Fact]
    public async Task The_tenants_own_key_is_used_and_the_ticket_context_reaches_the_model()
    {
        var (svc, model, _, id) = await BuildAsync();
        var answer = await svc.AskAsync(id, AssistantAction.NextSteps, null);

        model.Key.Should().Be("KEY-123");
        model.Prompt.Should().Contain("Outlook will not connect").And.Contain("WAITING_CUSTOMER");
        answer.IsDraft.Should().BeFalse("next steps are advice for the technician, not text to send");
    }

    [Fact]
    public async Task Draft_actions_are_marked_as_drafts_so_the_ui_never_treats_them_as_sent()
    {
        var (svc, _, _, id) = await BuildAsync();
        (await svc.AskAsync(id, AssistantAction.DraftReply, null)).IsDraft.Should().BeTrue();
        (await svc.AskAsync(id, AssistantAction.ImproveDraft, "my rough words")).IsDraft.Should().BeTrue();
    }

    [Fact]
    public async Task Improving_nothing_is_refused_rather_than_silently_inventing_a_reply()
    {
        var (svc, _, _, id) = await BuildAsync();
        var act = async () => await svc.AskAsync(id, AssistantAction.ImproveDraft, "   ");
        (await act.Should().ThrowAsync<ValidationFailedException>()).Which.Message.Should().Contain("Write something first");
    }

    [Fact]
    public async Task Improve_carries_the_technicians_own_words_through()
    {
        var (svc, model, _, id) = await BuildAsync();
        await svc.AskAsync(id, AssistantAction.ImproveDraft, "we fixd yr mailbox pls check");
        model.Prompt.Should().Contain("we fixd yr mailbox pls check");
    }

    [Fact]
    public async Task Switched_off_or_keyless_refuses_before_anything_is_sent()
    {
        var (off, offModel, _, offId) = await BuildAsync(enabled: false);
        (await off.AvailabilityAsync()).Enabled.Should().BeFalse();
        await Assert.ThrowsAsync<ValidationFailedException>(() => off.AskAsync(offId, AssistantAction.Summarise, null));
        offModel.Prompt.Should().BeNull("nothing may reach the provider while the feature is off");

        var (noKey, noKeyModel, _, noKeyId) = await BuildAsync(withKey: false);
        var avail = await noKey.AvailabilityAsync();
        avail.Enabled.Should().BeFalse();
        avail.Reason.Should().Contain("API key");
        await Assert.ThrowsAsync<ValidationFailedException>(() => noKey.AskAsync(noKeyId, AssistantAction.Summarise, null));
        noKeyModel.Prompt.Should().BeNull();
    }
}
