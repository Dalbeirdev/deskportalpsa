using System.Text;
using Desk.Application.Abstractions;
using Desk.Application.Connectors;
using Desk.Application.Sync;
using Desk.Domain.Enums;
using Desk.Domain.Sync;
using Desk.Infrastructure.Persistence;
using Desk.PsaCore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Desk.Api.Controllers;

/// <summary>
/// Inbound webhook receiver. Unauthenticated by design — PSAs cannot present a user session — so
/// trust is established by the connector's signature + timestamp validation, not by a login. The
/// connection id in the path is an unguessable GUID; the HMAC is the actual gate.
/// </summary>
[ApiController]
[Route("api/webhooks")]
[AllowAnonymous]
public sealed class WebhooksController(
    IConnectorResolver connectors,
    ISyncEventStore syncEvents,
    ISettableTenantContext tenant,
    DeskDbContext db,
    TimeProvider clock,
    ILogger<WebhooksController> logger) : ControllerBase
{
    [HttpPost("{connectionId:guid}")]
    public async Task<IActionResult> Receive(Guid connectionId, CancellationToken ct)
    {
        // Load the connection across tenants (no user session here), then act under its tenant.
        tenant.SetPlatformScope();

        var connection = await db.PsaConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == connectionId && c.IsEnabled, ct);
        if (connection is null)
        {
            logger.LogWarning("Webhook for unknown/disabled connection {ConnectionId}", connectionId);
            return NotFound();
        }

        var body = await ReadBodyAsync();
        var request = new WebhookRequest(
            Headers: Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase),
            Body: body,
            RawSignature: Request.Headers["X-Signature"].FirstOrDefault(),
            ReceivedAt: clock.GetUtcNow());

        var connector = await connectors.ResolveAsync(connectionId, ct);

        var validation = await connector.ValidateWebhookAsync(request, ct);
        if (!validation.IsValid)
        {
            // Security-relevant: a failed signature is logged and rejected, never processed.
            logger.LogWarning("Webhook signature validation failed for {ConnectionId}: {Reason}",
                connectionId, validation.Reason);
            return Unauthorized();
        }

        NormalizedProviderEvent evt;
        try
        {
            evt = await connector.ProcessWebhookAsync(request, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to normalize webhook payload for {ConnectionId}", connectionId);
            return BadRequest();
        }

        // Idempotency: duplicate deliveries are acknowledged but not re-processed.
        var registered = await syncEvents.TryRegisterAsync(new SyncEventRegistration
        {
            MspOrganizationId = connection.MspOrganizationId,
            PsaConnectionId = connectionId,
            EventType = evt.EventType,
            IdempotencyKey = evt.IdempotencyKey,
            SourceMarker = SyncSource.Provider,
            OccurredAt = evt.OccurredAt,
        }, ct);

        if (!registered)
            return Ok(new { status = "duplicate" });

        // Hand off to the worker (concrete sync handlers land in Phase 4).
        db.BackgroundJobs.Add(new BackgroundJob
        {
            MspOrganizationId = connection.MspOrganizationId,
            JobType = "sync.inbound-event",
            PayloadJson = $"{{\"connectionId\":\"{connectionId}\",\"idempotencyKey\":\"{evt.IdempotencyKey}\"}}",
            Status = BackgroundJobStatus.Queued,
        });
        await db.SaveChangesAsync(ct);

        return Accepted(new { status = "accepted", evt.EventType });
    }

    private async Task<string> ReadBodyAsync()
    {
        Request.EnableBuffering();
        Request.Body.Position = 0;
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        Request.Body.Position = 0;
        return body;
    }
}
