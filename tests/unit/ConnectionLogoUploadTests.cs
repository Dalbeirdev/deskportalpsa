using Desk.Application.Admin;
using Desk.Application.Attachments;
using Desk.Application.Common;
using Desk.Domain.Enums;
using Desk.Infrastructure.Admin;
using Desk.Infrastructure.Attachments;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// A logo is bytes an administrator supplied, served back from our own origin. What the upload
/// refuses is therefore a security boundary, not a formatting preference.
/// </summary>
public class ConnectionLogoUploadTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static async Task<(ConnectionAdminService Svc, Guid Id, DeskDbContextHolder H)> SetupAsync()
    {
        var h = AdminHarness.Create(Guid.NewGuid());
        var audit = new AuditWriter(h.Db, h.User, h.Tenant, h.Clock);
        var svc = new ConnectionAdminService(h.Db, h.Secrets, audit,
            new StubResolver(), new ConnectionFieldCache(),
            new InMemoryObjectStorage(new AttachmentStorageOptions(), h.Clock), h.Clock);

        var created = await svc.CreateAsync(new CreateConnectionInput(
            "CWM", ProviderType.ConnectWisePsa, "https://api-na.myconnectwise.net/v4_6_release/apis/3.0/",
            null, new Dictionary<string, string> { ["CompanyId"] = "x" }, null));
        return (svc, created.Id, new DeskDbContextHolder(h));
    }

    private sealed record DeskDbContextHolder(AdminHarness H);

    private sealed class StubResolver : Desk.Application.Connectors.IConnectorResolver
    {
        public Task<Desk.PsaCore.Contracts.IServiceManagementConnector> ResolveAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<Desk.PsaCore.Contracts.IServiceManagementConnector>(new StubConnector());
    }

    [Fact]
    public async Task An_uploaded_logo_is_stored_and_served_back()
    {
        var (svc, id, holder) = await SetupAsync();
        await using var _ = holder.H.Db;

        var summary = await svc.UploadLogoAsync(id, new ConnectionLogoUpload("logo.png", "image/png", Png));

        summary.LogoUrl.Should().Contain($"/api/admin/connections/{id}/logo");
        var stored = await svc.GetLogoAsync(id);
        stored!.Content.Should().Equal(Png);
        stored.ContentType.Should().Be("image/png");
    }

    [Fact]
    public async Task An_svg_is_refused_because_it_can_carry_script()
    {
        // Not a style rule: an SVG opened directly renders as a document on this origin, which
        // would make an upload field a stored cross-site scripting vector.
        var (svc, id, holder) = await SetupAsync();
        await using var _ = holder.H.Db;

        var act = async () => await svc.UploadLogoAsync(id,
            new ConnectionLogoUpload("logo.svg", "image/svg+xml", "<svg onload=\"alert(1)\"/>"u8.ToArray()));

        await act.Should().ThrowAsync<ValidationFailedException>();
        (await svc.GetLogoAsync(id)).Should().BeNull();
    }

    [Theory]
    [InlineData("text/html")]
    [InlineData("application/javascript")]
    [InlineData("application/octet-stream")]
    [InlineData("")]
    public async Task Any_type_outside_the_raster_allowlist_is_refused(string contentType)
    {
        var (svc, id, holder) = await SetupAsync();
        await using var _ = holder.H.Db;

        var act = async () => await svc.UploadLogoAsync(id, new ConnectionLogoUpload("x", contentType, Png));
        await act.Should().ThrowAsync<ValidationFailedException>();
    }

    [Fact]
    public async Task A_file_over_the_limit_is_refused_rather_than_stored()
    {
        var (svc, id, holder) = await SetupAsync();
        await using var _ = holder.H.Db;

        var act = async () => await svc.UploadLogoAsync(id,
            new ConnectionLogoUpload("big.png", "image/png", new byte[1024 * 1024 + 1]));

        await act.Should().ThrowAsync<ValidationFailedException>();
        (await svc.GetLogoAsync(id)).Should().BeNull();
    }

    [Fact]
    public async Task Removing_a_logo_clears_both_the_reference_and_the_stored_object()
    {
        var (svc, id, holder) = await SetupAsync();
        await using var _ = holder.H.Db;
        await svc.UploadLogoAsync(id, new ConnectionLogoUpload("logo.png", "image/png", Png));

        await svc.RemoveLogoAsync(id);

        (await svc.GetLogoAsync(id)).Should().BeNull();
        var row = await holder.H.Db.PsaConnections.AsNoTracking().SingleAsync(c => c.Id == id);
        row.LogoUrl.Should().BeNull();
        row.LogoStorageKey.Should().BeNull();
    }

    [Fact]
    public async Task Replacing_a_logo_changes_the_url_so_a_cache_cannot_serve_the_old_one()
    {
        var (svc, id, holder) = await SetupAsync();
        await using var _ = holder.H.Db;

        var first = await svc.UploadLogoAsync(id, new ConnectionLogoUpload("a.png", "image/png", Png));
        holder.H.Clock.Advance(TimeSpan.FromSeconds(5));
        var second = await svc.UploadLogoAsync(id, new ConnectionLogoUpload("b.png", "image/png", Png));

        second.LogoUrl.Should().NotBe(first.LogoUrl);
    }
}
