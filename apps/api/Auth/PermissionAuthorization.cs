using Desk.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;

namespace Desk.Api.Auth;

/// <summary>Authorization requirement satisfied when the caller holds a specific permission claim.</summary>
public sealed class PermissionRequirement(string permissionKey) : IAuthorizationRequirement
{
    public string PermissionKey { get; } = permissionKey;
}

public sealed class PermissionAuthorizationHandler(ICurrentUser currentUser)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (currentUser.HasPermission(requirement.PermissionKey))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Builds authorization policies on demand for names of the form "perm:&lt;key&gt;", so a
/// controller can declare <c>[Authorize(Policy = Policy.For(Permissions.X)]</c> without
/// registering every policy up front.
/// </summary>
public sealed class PermissionPolicyProvider(Microsoft.Extensions.Options.IOptions<AuthorizationOptions> options)
    : IAuthorizationPolicyProvider
{
    public const string Prefix = "perm:";
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public static string For(string permissionKey) => Prefix + permissionKey;

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName[Prefix.Length..]))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }
        return _fallback.GetPolicyAsync(policyName);
    }
}

/// <summary>Convenience attribute: <c>[RequirePermission(Permissions.TicketsCreate)]</c>.</summary>
public sealed class RequirePermissionAttribute : Microsoft.AspNetCore.Authorization.AuthorizeAttribute
{
    public RequirePermissionAttribute(string permissionKey) => Policy = PermissionPolicyProvider.For(permissionKey);
}
