using Desk.Domain.Marketing;

namespace Desk.Application.Marketing;

/// <param name="Website">Honeypot. A real browser leaves it empty because it is hidden; bots fill
/// every field they find. Non-empty means the submission is accepted with a normal response and
/// then dropped, so the bot learns nothing from being blocked.</param>
public sealed record SubmitEnquiryInput(
    EnquiryKind Kind,
    string Name,
    string Email,
    string? Company,
    string? Phone,
    string Message,
    string? PreferredTime,
    string? SourcePage,
    string? Website);

public sealed record EnquiryDto(
    Guid Id,
    EnquiryKind Kind,
    string Name,
    string Email,
    string? Company,
    string? Phone,
    string Message,
    string? PreferredTime,
    EnquiryStatus Status,
    string? SourcePage,
    DateTimeOffset CreatedAt);

public sealed record EnquiryListResult(int Total, int NewCount, IReadOnlyList<EnquiryDto> Items);

public interface IEnquiryService
{
    /// <summary>Anonymous submission from the public site. Validates, then stores.</summary>
    Task<bool> SubmitAsync(SubmitEnquiryInput input, CancellationToken ct = default);

    Task<EnquiryListResult> ListAsync(EnquiryStatus? status = null, CancellationToken ct = default);

    Task<bool> SetStatusAsync(Guid id, EnquiryStatus status, CancellationToken ct = default);
}
