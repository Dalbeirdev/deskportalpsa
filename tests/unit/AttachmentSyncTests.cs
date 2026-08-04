using System.Text;
using Desk.Application.Attachments;
using Desk.Application.Mapping;
using Desk.Application.Sync;
using Desk.Application.Tickets;
using Desk.Domain.Enums;
using Desk.Domain.Tenancy;
using Desk.Domain.Tickets;
using Desk.Infrastructure.Admin;
using Desk.Infrastructure.Attachments;
using Desk.Infrastructure.Persistence;
using Desk.Infrastructure.Sync;
using Desk.PsaCore.Contracts;
using Desk.PsaCore.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// Attachment sync in both directions: a portal upload reaches the PSA with its real file name and
/// bytes, provider files come back scanned and downloadable, and neither side echoes the other.
/// </summary>
public class AttachmentSyncTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Conn = Guid.NewGuid();

    private sealed class FakeResolver(IServiceManagementConnector c) : Desk.Application.Connectors.IConnectorResolver
    {
        public Task<IServiceManagementConnector> ResolveAsync(Guid id, CancellationToken ct = default) => Task.FromResult(c);
    }

    private static readonly byte[] Png =
        [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    private sealed record Fixture(
        DeskDbContext Db, StubConnector Connector, AttachmentService Service,
        InMemoryObjectStorage Storage, TestClock Clock, Guid TicketId);

    private static async Task<Fixture> SetupAsync(bool syncAttachments = true, string? externalTicketId = "7810")
    {
        var h = AdminHarness.Create(Org);
        var clock = h.Clock;
        var db = h.Db;
        db.PsaConnections.Add(new PsaConnection
        {
            Id = Conn, MspOrganizationId = Org, Name = "AT", Provider = ProviderType.AutotaskPsa,
            ApiEndpoint = "https://x", CredentialSecretRef = "mem://x", SyncAttachments = syncAttachments,
        });
        var ticket = new Ticket
        {
            MspOrganizationId = Org, PsaConnectionId = Conn, Provider = ProviderType.AutotaskPsa,
            ExternalTicketId = externalTicketId, ClientCompanyId = Guid.NewGuid(),
            RequesterName = "R", RequesterEmail = "r@x", Title = "t",
            PortalStatus = "NEW", PortalPriority = "NORMAL",
        };
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        var connector = new StubConnector();
        var storage = new InMemoryObjectStorage(new AttachmentStorageOptions(), clock);
        var service = new AttachmentService(db, storage, new HeuristicMalwareScanner(), new FakeResolver(connector),
            new AuditWriter(db, h.User, h.Tenant, clock), new AttachmentPolicy(), clock);
        return new Fixture(db, connector, service, storage, clock, ticket.Id);
    }

    private static ConnectionSyncRunner Runner(Fixture f) =>
        new(f.Db, new FakeResolver(f.Connector),
            new TicketSyncService(f.Db, new MappingEngine(), new SyncEventStore(f.Db, f.Clock), f.Clock),
            f.Storage, new HeuristicMalwareScanner(), f.Clock);

    [Fact]
    public async Task A_portal_upload_is_pushed_to_the_provider_with_its_real_file_name()
    {
        var f = await SetupAsync();
        await using var _ = f.Db;

        await f.Service.UploadAsync(new UploadAttachmentInput(f.TicketId, Org, "test-image.png", "image/png", Png));

        var (ticketId, pushed) = f.Connector.Uploaded.Should().ContainSingle().Subject;
        ticketId.Should().Be("7810");
        // The randomized storage key is an internal detail; sending it would name the provider-side
        // download after a GUID.
        pushed.FileName.Should().Be("test-image.png");
        pushed.Content.Should().Equal(Png);
        pushed.ContentType.Should().Be("image/png");

        var row = await f.Db.TicketAttachments.SingleAsync();
        row.ExternalAttachmentId.Should().NotBeNull();
        row.PushedToProviderAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Nothing_is_pushed_when_the_connection_disables_attachment_sync()
    {
        var f = await SetupAsync(syncAttachments: false);
        await using var _ = f.Db;

        await f.Service.UploadAsync(new UploadAttachmentInput(f.TicketId, Org, "a.png", "image/png", Png));

        f.Connector.Uploaded.Should().BeEmpty();
        (await f.Db.TicketAttachments.SingleAsync()).ExternalAttachmentId.Should().BeNull();
    }

    [Fact]
    public async Task An_infected_upload_is_never_pushed_to_the_provider()
    {
        var f = await SetupAsync();
        await using var _ = f.Db;
        var eicar = Encoding.ASCII.GetBytes(@"X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*");

        await f.Service.UploadAsync(new UploadAttachmentInput(f.TicketId, Org, "bad.txt", "text/plain", eicar));

        f.Connector.Uploaded.Should().BeEmpty();
        (await f.Db.TicketAttachments.SingleAsync()).ScanStatus.Should().Be(AttachmentScanStatus.Quarantined);
    }

    [Fact]
    public async Task A_provider_upload_failure_does_not_fail_the_customers_upload()
    {
        var f = await SetupAsync(externalTicketId: null); // not yet synced, so there is nowhere to push
        await using var _ = f.Db;

        var dto = await f.Service.UploadAsync(new UploadAttachmentInput(f.TicketId, Org, "a.png", "image/png", Png));

        dto.FileName.Should().Be("a.png");
        (await f.Db.TicketAttachments.SingleAsync()).ScanStatus.Should().Be(AttachmentScanStatus.Clean);
    }

    [Fact]
    public async Task Provider_files_are_imported_once_with_their_bytes_and_author()
    {
        var f = await SetupAsync();
        await using var _ = f.Db;
        f.Connector.Tickets.Add(new UnifiedTicket
        {
            ExternalId = "7810", Title = "t", Status = "1", Priority = "1",
            RequesterExternalId = "176", RequesterName = "Acme", RequesterEmail = "a@acme.test",
        });
        f.Connector.Attachments["7810"] =
        [
            (new UnifiedAttachment("38", "autotask-image.png", "image/png", Png.Length)
                { CreatedAt = f.Clock.GetUtcNow(), AuthorName = "Jane Tech" }, Png),
        ];

        var first = await Runner(f).RunAsync(Conn, full: true);
        var second = await Runner(f).RunAsync(Conn, full: true);

        first.Attachments.Should().Be(1);
        second.Attachments.Should().Be(0); // re-running must not duplicate the file list
        var row = await f.Db.TicketAttachments.SingleAsync();
        row.OriginalFileName.Should().Be("autotask-image.png");
        row.ImportedFromProvider.Should().BeTrue();
        row.AuthorName.Should().Be("Jane Tech");
        row.ScanStatus.Should().Be(AttachmentScanStatus.Clean);
        (await f.Storage.GetAsync(row.StorageObjectKey)).Should().Equal(Png); // bytes really landed
    }

    [Fact]
    public async Task A_file_uploaded_here_is_not_re_imported_as_an_echo()
    {
        var f = await SetupAsync();
        await using var _ = f.Db;
        f.Connector.Tickets.Add(new UnifiedTicket
        {
            ExternalId = "7810", Title = "t", Status = "1", Priority = "1",
            RequesterExternalId = "176", RequesterName = "Acme", RequesterEmail = "a@acme.test",
        });

        await f.Service.UploadAsync(new UploadAttachmentInput(f.TicketId, Org, "test-image.png", "image/png", Png));
        var externalId = (await f.Db.TicketAttachments.SingleAsync()).ExternalAttachmentId!;
        // The provider now reports the file the portal just pushed to it.
        f.Connector.Attachments["7810"] =
        [
            (new UnifiedAttachment(externalId, "test-image.png", "image/png", Png.Length)
                { CreatedAt = f.Clock.GetUtcNow(), AuthorName = "Portal" }, Png),
        ];

        var run = await Runner(f).RunAsync(Conn, full: true);

        run.Attachments.Should().Be(0);
        var row = await f.Db.TicketAttachments.SingleAsync();
        row.ImportedFromProvider.Should().BeFalse(); // still recorded as the customer's own upload
    }

    [Fact]
    public async Task Attachments_are_swept_once_per_run_not_once_per_ticket()
    {
        var f = await SetupAsync();
        await using var _ = f.Db;
        foreach (var id in new[] { "7810", "7811", "7812" })
            f.Connector.Tickets.Add(new UnifiedTicket
            {
                ExternalId = id, Title = "t", Status = "1", Priority = "1",
                RequesterExternalId = "176", RequesterName = "Acme", RequesterEmail = "a@acme.test",
            });

        await Runner(f).RunAsync(Conn, full: true);

        // Providers do not reliably bump a ticket's timestamp when a file is attached, so the sweep
        // is dated and tenant-wide — one call, regardless of how many tickets the page held.
        f.Connector.AttachmentSweeps.Should().Be(1);
    }

    [Fact]
    public async Task A_provider_file_with_no_retrievable_bytes_is_skipped_rather_than_recorded()
    {
        var f = await SetupAsync();
        await using var _ = f.Db;
        f.Connector.Tickets.Add(new UnifiedTicket
        {
            ExternalId = "7810", Title = "t", Status = "1", Priority = "1",
            RequesterExternalId = "176", RequesterName = "Acme", RequesterEmail = "a@acme.test",
        });
        f.Connector.Attachments["7810"] =
        [
            (new UnifiedAttachment("99", "empty.bin", "application/octet-stream", 0) { CreatedAt = f.Clock.GetUtcNow() }, []),
        ];

        var run = await Runner(f).RunAsync(Conn, full: true);

        // A row without bytes is an undownloadable entry in the customer's list — worse than absent.
        run.Attachments.Should().Be(0);
        (await f.Db.TicketAttachments.CountAsync()).Should().Be(0);
    }
}
