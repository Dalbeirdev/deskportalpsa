using System.Net.Mail;
using Desk.Application.Marketing;
using Desk.Domain.Marketing;
using Desk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Desk.Infrastructure.Marketing;

/// <summary>
/// Inbound enquiries from the public site. Submission is anonymous, so everything it accepts is
/// treated as hostile: length-capped, trimmed, and never echoed back to the caller.
/// </summary>
public sealed class EnquiryService(DeskDbContext db, TimeProvider clock) : IEnquiryService
{
    private static string? Clip(string? value, int max)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        return v.Length <= max ? v : v[..max];
    }

    private static bool LooksLikeEmail(string email) =>
        MailAddress.TryCreate(email, out var parsed) && parsed.Host.Contains('.');

    public async Task<bool> SubmitAsync(SubmitEnquiryInput input, CancellationToken ct = default)
    {
        // Honeypot tripped: answer as though it worked. Telling a bot it was caught only teaches it
        // which field to leave alone next time.
        if (!string.IsNullOrWhiteSpace(input.Website)) return true;

        var name = Clip(input.Name, 120);
        var email = Clip(input.Email, 200);
        var message = Clip(input.Message, 4000);
        if (name is null || email is null || message is null || !LooksLikeEmail(email))
            return false;

        var company = Clip(input.Company, 160);
        var phone = Clip(input.Phone, 60);
        var preferred = Clip(input.PreferredTime, 200);

        // A meeting request needs someone reachable and a time to aim for; a general question does
        // not. The browser marks the same fields required, but that is a courtesy to the visitor —
        // this endpoint is anonymous and anything arriving here may have skipped the form entirely.
        if (input.Kind == EnquiryKind.Meeting && (company is null || phone is null || preferred is null))
            return false;

        db.Enquiries.Add(new Enquiry
        {
            Kind = input.Kind,
            Name = name,
            Email = email,
            Company = company,
            Phone = phone,
            Message = message,
            PreferredTime = preferred,
            SourcePage = Clip(input.SourcePage, 200),
            Status = EnquiryStatus.New,
            CreatedAt = clock.GetUtcNow(),
            UpdatedAt = clock.GetUtcNow(),
        });
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<EnquiryListResult> ListAsync(EnquiryStatus? status = null, CancellationToken ct = default)
    {
        var q = db.Enquiries.AsNoTracking();
        var total = await q.CountAsync(ct);
        var newCount = await q.CountAsync(e => e.Status == EnquiryStatus.New, ct);
        if (status is { } s) q = q.Where(e => e.Status == s);

        var items = await q
            .OrderByDescending(e => e.CreatedAt)
            .Take(200)
            .Select(e => new EnquiryDto(
                e.Id, e.Kind, e.Name, e.Email, e.Company, e.Phone, e.Message,
                e.PreferredTime, e.Status, e.SourcePage, e.CreatedAt))
            .ToListAsync(ct);

        return new EnquiryListResult(total, newCount, items);
    }

    public async Task<bool> SetStatusAsync(Guid id, EnquiryStatus status, CancellationToken ct = default)
    {
        var row = await db.Enquiries.SingleOrDefaultAsync(e => e.Id == id, ct);
        if (row is null) return false;
        row.Status = status;
        row.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return true;
    }
}
