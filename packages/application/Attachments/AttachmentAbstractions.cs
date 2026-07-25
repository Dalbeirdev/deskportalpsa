using Desk.Application.Tickets;

namespace Desk.Application.Attachments;

/// <summary>Object storage seam. Production binds this to MinIO/S3; tests use an in-memory impl.</summary>
public interface IObjectStorage
{
    Task PutAsync(string key, byte[] data, string contentType, CancellationToken ct = default);
    Task<byte[]?> GetAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
    /// <summary>A time-limited, tamper-evident URL to fetch the object without a session.</summary>
    Task<string> PresignGetAsync(string key, TimeSpan ttl, CancellationToken ct = default);
}

public sealed record ScanOutcome(bool IsClean, string? Detail)
{
    public static ScanOutcome Clean { get; } = new(true, null);
    public static ScanOutcome Infected(string detail) => new(false, detail);
}

/// <summary>Scans uploaded bytes for malware. Default impl is heuristic (EICAR); production wires ClamAV.</summary>
public interface IMalwareScanner
{
    Task<ScanOutcome> ScanAsync(byte[] data, string fileName, CancellationToken ct = default);
}

public sealed record UploadAttachmentInput(
    Guid TicketId,
    Guid MspOrganizationId,
    string FileName,
    string ContentType,
    byte[] Content);

public interface IAttachmentService
{
    /// <summary>Validates, scans, stores (if clean) or quarantines (if not), and records metadata.</summary>
    Task<AttachmentDto> UploadAsync(UploadAttachmentInput input, CancellationToken ct = default);

    /// <summary>Issues a short-lived signed URL for a CLEAN attachment and audits the download. Null if not downloadable.</summary>
    Task<string?> GetDownloadUrlAsync(Guid attachmentId, CancellationToken ct = default);
}

/// <summary>Upload validation policy: size cap, and denylists for dangerous extensions and content types.</summary>
public sealed class AttachmentPolicy
{
    public long MaxBytes { get; init; } = 25 * 1024 * 1024;
    public TimeSpan DownloadUrlTtl { get; init; } = TimeSpan.FromMinutes(5);

    public ISet<string> BlockedExtensions { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".com", ".bat", ".cmd", ".scr", ".msi", ".msp", ".cpl", ".hta", ".jar",
        ".js", ".jse", ".vbs", ".vbe", ".wsf", ".wsh", ".ps1", ".psm1", ".reg", ".lnk", ".sh",
        ".app", ".gadget", ".pif", ".vb", ".apk", ".dmg",
    };

    public ISet<string> BlockedContentTypes { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "application/x-msdownload", "application/x-msdos-program", "application/x-executable",
        "application/x-dosexec", "application/vnd.microsoft.portable-executable", "application/x-sh",
    };

    /// <summary>Returns null if acceptable, otherwise a human-readable rejection reason.</summary>
    public string? Validate(string fileName, string contentType, long size)
    {
        if (size <= 0) return "The file is empty.";
        if (size > MaxBytes) return $"The file exceeds the {MaxBytes / (1024 * 1024)} MB limit.";

        var ext = System.IO.Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext)) return "The file has no extension.";
        if (BlockedExtensions.Contains(ext)) return $"Files of type '{ext}' are not permitted.";

        // Reject a dangerous second extension (e.g. invoice.pdf.exe).
        var doubleExt = System.IO.Path.GetExtension(System.IO.Path.GetFileNameWithoutExtension(fileName));
        if (!string.IsNullOrEmpty(doubleExt) && BlockedExtensions.Contains(doubleExt))
            return "Double file extensions are not permitted.";

        if (BlockedContentTypes.Contains(contentType)) return "Executable content types are not permitted.";
        return null;
    }
}
