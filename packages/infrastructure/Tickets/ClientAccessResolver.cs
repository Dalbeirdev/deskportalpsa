using Desk.Application.Tickets;
using Desk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Desk.Infrastructure.Tickets;

/// <summary>Maps an authenticated subject to its client company + user, or null for non-client callers.</summary>
public sealed class ClientAccessResolver(DeskDbContext db) : IClientAccessResolver
{
    public async Task<ClientAccess?> ResolveAsync(string idpSubject, CancellationToken ct = default)
    {
        var user = await db.ClientUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.IdpSubject == idpSubject && u.IsActive, ct);

        return user is null
            ? null
            : new ClientAccess(user.MspOrganizationId, user.ClientCompanyId, user.Id, user.IsCompanyAdministrator);
    }
}
