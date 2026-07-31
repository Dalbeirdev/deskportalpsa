using Desk.Application.ControlPanel;
using Desk.Domain.ControlPanel;
using Desk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Desk.Infrastructure.ControlPanel;

/// <summary>
/// Default report delivery: logs and reports "not delivered by email". Reports are always stored for
/// in-portal download, so scheduling works fully without an email provider. Swap this for an SMTP /
/// provider-backed implementation (environment-gated) to enable actual email delivery.
/// </summary>
public sealed class LoggingReportDelivery(ILogger<LoggingReportDelivery> logger) : IReportDelivery
{
    public Task<ReportDeliveryResult> DeliverAsync(string? recipients, string subject, string fileName, string csv, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(recipients))
            return Task.FromResult(new ReportDeliveryResult(false, "No recipients set — report available in the portal."));

        logger.LogInformation("Scheduled report '{Subject}' ({Bytes} bytes) queued for {Recipients} (email delivery not configured).",
            subject, csv.Length, recipients);
        return Task.FromResult(new ReportDeliveryResult(false, "Email delivery is not configured; report available for download in the portal."));
    }
}

/// <summary>
/// Generates + delivers reports for every enabled schedule that is due. Runs under whatever scope the
/// caller establishes — the worker sets platform scope so this sees schedules across all tenants.
/// </summary>
public sealed class ScheduledReportRunner(
    DeskDbContext db,
    TimeProvider clock,
    IReportDelivery delivery,
    ILogger<ScheduledReportRunner> logger) : IScheduledReportRunner
{
    public async Task<int> RunDueAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var due = await db.ReportSchedules
            .Where(s => s.IsEnabled && s.NextRunAt <= now)
            .OrderBy(s => s.NextRunAt).Take(200)
            .ToListAsync(ct);

        var processed = 0;
        foreach (var schedule in due)
        {
            try
            {
                var report = await ReportComposer.BuildAsync(db, schedule.ClientCompanyId, ct);
                var company = await db.ClientCompanies.AsNoTracking()
                    .Where(c => c.Id == schedule.ClientCompanyId).Select(c => c.Name).FirstOrDefaultAsync(ct) ?? "Account";
                var csv = ReportComposer.ToCsv(report, company, now);

                var run = new ReportRun
                {
                    MspOrganizationId = schedule.MspOrganizationId,
                    ClientCompanyId = schedule.ClientCompanyId,
                    ReportScheduleId = schedule.Id,
                    GeneratedAt = now,
                    Format = "csv",
                    Summary = ReportComposer.Summary(report),
                    Content = csv,
                };
                db.ReportRuns.Add(run);

                var result = await delivery.DeliverAsync(schedule.Recipients, $"{company} — scheduled report",
                    $"{company}-report.csv", csv, ct);
                run.Delivered = result.Delivered;
                run.DeliveryNote = result.Note;

                schedule.LastRunAt = now;
                schedule.NextRunAt = ReportComposer.Advance(now, schedule.Frequency);
                await db.SaveChangesAsync(ct);
                processed++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to generate scheduled report {ScheduleId}", schedule.Id);
            }
        }

        if (processed > 0) logger.LogInformation("Generated {Count} scheduled report(s)", processed);
        return processed;
    }
}
