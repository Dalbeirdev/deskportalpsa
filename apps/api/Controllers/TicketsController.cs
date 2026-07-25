using System.ComponentModel.DataAnnotations;
using Desk.Api.Auth;
using Desk.Application.Abstractions;
using Desk.Application.Common;
using Desk.Application.Tickets;
using Desk.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Desk.Api.Controllers;

/// <summary>
/// Client-portal ticket endpoints. Every action resolves the caller's client identity and is scoped
/// to their company (and their own tickets unless they are a company administrator). Detail returns
/// only the public conversation — internal PSA notes are never exposed.
/// </summary>
[ApiController]
[Route("api/tickets")]
[Authorize]
public sealed class TicketsController(
    ICurrentUser user,
    IClientAccessResolver accessResolver,
    ITicketReadService reads,
    ITicketCommandService commands) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await reads.ListAsync(await AccessAsync(ct), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var detail = await reads.GetDetailAsync(await AccessAsync(ct), id, ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPost]
    [RequirePermission(Permissions.TicketsCreate)]
    public async Task<IActionResult> Create([FromBody] CreateTicketRequest req, CancellationToken ct)
    {
        var result = await commands.CreateAsync(await AccessAsync(ct),
            new CreateTicketInput(req.Title, req.Description, req.Priority, req.Category, req.QueueOrBoard), ct);
        return CreatedAtAction(nameof(Detail), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/comments")]
    [RequirePermission(Permissions.TicketsAddPublicNote)]
    public async Task<IActionResult> Comment(Guid id, [FromBody] AddCommentRequest req, CancellationToken ct)
        => Ok(await commands.AddCommentAsync(await AccessAsync(ct), id, req.Body, ct));

    // Resolves the client identity or refuses the request — staff use the dashboard endpoints instead.
    private async Task<ClientAccess> AccessAsync(CancellationToken ct)
        => await accessResolver.ResolveAsync(user.Subject ?? "", ct)
           ?? throw new ForbiddenException("This endpoint is for client portal users.");

    public sealed record CreateTicketRequest(
        [property: Required, StringLength(500, MinimumLength = 3)] string Title,
        string? Description,
        string? Priority,
        string? Category,
        string? QueueOrBoard);

    public sealed record AddCommentRequest(
        [property: Required, StringLength(10000, MinimumLength = 1)] string Body);
}
