using System.Text;
using Desk.Application.Attachments;
using Desk.Application.Common;
using Desk.Domain.Enums;
using Desk.Domain.Tickets;
using Desk.Infrastructure.Admin;
using Desk.Infrastructure.Attachments;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Desk.Tests.Unit;

public class AttachmentTests
{
    private static readonly Guid Org = Guid.NewGuid();

    private static readonly byte[] Eicar = Encoding.ASCII.GetBytes(
        @"X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*");

    private sealed class Fixture
    {
        public required AdminHarness H { get; init; }
        public required AttachmentService Svc { get; init; }
        public required InMemoryObjectStorage Storage { get; init; }
        public required Guid TicketId { get; init; }
    }

    private static async Task<Fixture> SetupAsync(AttachmentPolicy? policy = null)
    {
        var h = AdminHarness.Create(Org);
        var ticket = new Ticket
        {
            MspOrganizationId = Org, PsaConnectionId = Guid.NewGuid(), Provider = ProviderType.AutotaskPsa,
            ClientCompanyId = Guid.NewGuid(), RequesterName = "R", RequesterEmail = "r@x", Title = "t",
            PortalStatus = "NEW", PortalPriority = "NORMAL",
        };
        h.Db.Tickets.Add(ticket);
        await h.Db.SaveChangesAsync();

        var storage = new InMemoryObjectStorage(new AttachmentStorageOptions(), h.Clock);
        var svc = new AttachmentService(h.Db, storage, new HeuristicMalwareScanner(),
            new AuditWriter(h.Db, h.User, h.Tenant, h.Clock), policy ?? new AttachmentPolicy(), h.Clock);
        return new Fixture { H = h, Svc = svc, Storage = storage, TicketId = ticket.Id };
    }

    private static UploadAttachmentInput Upload(Guid ticketId, string name, string type, byte[] content)
        => new(ticketId, Org, name, type, content);

    [Fact]
    public async Task Clean_file_is_stored_scanned_clean_and_downloadable()
    {
        var f = await SetupAsync();
        var dto = await f.Svc.UploadAsync(Upload(f.TicketId, "report.pdf", "application/pdf", Encoding.UTF8.GetBytes("PDF body")));

        dto.ScanStatus.Should().Be(AttachmentScanStatus.Clean);
        var row = await f.H.Db.TicketAttachments.SingleAsync();
        (await f.Storage.GetAsync(row.StorageObjectKey)).Should().NotBeNull();

        var url = await f.Svc.GetDownloadUrlAsync(dto.Id);
        url.Should().NotBeNull().And.Contain("sig=");
        (await f.H.Db.AuditLog.CountAsync(a => a.Action == "attachment.uploaded")).Should().Be(1);
    }

    [Fact]
    public async Task Infected_file_is_quarantined_not_stored_and_not_downloadable()
    {
        var f = await SetupAsync();
        var dto = await f.Svc.UploadAsync(Upload(f.TicketId, "notes.txt", "text/plain", Eicar));

        dto.ScanStatus.Should().Be(AttachmentScanStatus.Quarantined);
        var row = await f.H.Db.TicketAttachments.SingleAsync();
        row.StorageObjectKey.Should().BeEmpty();                 // bytes never written
        row.ScanDetail.Should().Contain("EICAR");

        (await f.Svc.GetDownloadUrlAsync(dto.Id)).Should().BeNull(); // never downloadable
        (await f.H.Db.AuditLog.CountAsync(a => a.Action == "attachment.quarantined")).Should().Be(1);
    }

    [Fact]
    public async Task Executable_extension_is_rejected()
    {
        var f = await SetupAsync();
        var act = async () => await f.Svc.UploadAsync(Upload(f.TicketId, "malware.exe", "application/octet-stream", [1, 2, 3]));
        await act.Should().ThrowAsync<ValidationFailedException>();
        (await f.H.Db.TicketAttachments.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Double_extension_is_rejected()
    {
        var f = await SetupAsync();
        var act = async () => await f.Svc.UploadAsync(Upload(f.TicketId, "invoice.pdf.exe", "application/pdf", [1]));
        await act.Should().ThrowAsync<ValidationFailedException>();
    }

    [Fact]
    public async Task Oversize_file_is_rejected()
    {
        var f = await SetupAsync(new AttachmentPolicy { MaxBytes = 10 });
        var act = async () => await f.Svc.UploadAsync(Upload(f.TicketId, "big.txt", "text/plain", new byte[20]));
        await act.Should().ThrowAsync<ValidationFailedException>();
    }

    [Fact]
    public async Task Storage_key_is_randomized_not_derived_from_filename()
    {
        var f = await SetupAsync();
        await f.Svc.UploadAsync(Upload(f.TicketId, "report.pdf", "application/pdf", Encoding.UTF8.GetBytes("a")));
        await f.Svc.UploadAsync(Upload(f.TicketId, "report.pdf", "application/pdf", Encoding.UTF8.GetBytes("b")));

        var keys = await f.H.Db.TicketAttachments.Select(a => a.StorageObjectKey).ToListAsync();
        keys.Should().OnlyHaveUniqueItems();                       // same filename -> different keys
        keys.Should().OnlyContain(k => !k.Contains("report"));     // filename not leaked into the key
    }

    [Fact]
    public async Task Presigned_url_rejects_tampering_and_expiry()
    {
        var f = await SetupAsync();
        var url = await f.Storage.PresignGetAsync("att/x/abc.pdf", TimeSpan.FromMinutes(5));
        var (key, exp, sig) = ParseUrl(url);
        var opts = new AttachmentStorageOptions();

        InMemoryObjectStorage.VerifySignature(key, exp, sig, opts.SigningKey, f.H.Clock.GetUtcNow()).Should().BeTrue();
        InMemoryObjectStorage.VerifySignature(key, exp, "deadbeef", opts.SigningKey, f.H.Clock.GetUtcNow()).Should().BeFalse();
        // After expiry the same signature is refused.
        var later = f.H.Clock.GetUtcNow().AddMinutes(6);
        InMemoryObjectStorage.VerifySignature(key, exp, sig, opts.SigningKey, later).Should().BeFalse();
    }

    private static (string key, long exp, string sig) ParseUrl(string url)
    {
        var query = url[(url.IndexOf('?') + 1)..].Split('&')
            .Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
        return (query["key"], long.Parse(query["exp"]), query["sig"]);
    }
}
