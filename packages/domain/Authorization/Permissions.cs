using Desk.Domain.Enums;

namespace Desk.Domain.Authorization;

/// <summary>
/// Central catalogue of permission-claim keys. Authorization is evaluated against these
/// claims — never against role names directly — so access can be recomposed without code
/// changes (spec §10: "Use permission claims rather than hard-coded role checks").
/// </summary>
public static class Permissions
{
    // Platform
    public const string PlatformManageOrganizations = "platform.organizations.manage";
    public const string PlatformViewAllHealth = "platform.health.view";
    public const string PlatformManageSettings = "platform.settings.manage";

    // Organization / connections / mapping
    public const string OrgManage = "org.manage";
    public const string ConnectionsManage = "connections.manage";
    public const string ConnectionsView = "connections.view";
    public const string MappingsManage = "mappings.manage";
    public const string MappingsView = "mappings.view";

    // Users & roles
    public const string UsersManage = "users.manage";
    public const string RolesManage = "roles.manage";
    public const string ClientUsersManage = "clientusers.manage";

    // Tickets
    public const string TicketsViewAll = "tickets.view.all";
    public const string TicketsViewAssigned = "tickets.view.assigned";
    public const string TicketsViewOwnCompany = "tickets.view.company";
    public const string TicketsViewOwn = "tickets.view.own";
    public const string TicketsCreate = "tickets.create";
    public const string TicketsAddPublicNote = "tickets.note.public.add";
    public const string TicketsLogTime = "tickets.time.log";
    public const string TicketsUpdate = "tickets.update";

    // Dashboards & reports
    public const string ReportsView = "reports.view";
    public const string ProductivityViewTeam = "productivity.team.view";
    public const string ProductivityViewOwn = "productivity.own.view";

    // Ops & audit
    public const string IntegrationHealthView = "integration.health.view";
    public const string JobsManage = "jobs.manage";
    public const string AuditView = "audit.view";
    public const string SecurityConfigView = "security.config.view";

    /// <summary>Every claim, used to grant the full set to super-administrators.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        PlatformManageOrganizations, PlatformViewAllHealth, PlatformManageSettings,
        OrgManage, ConnectionsManage, ConnectionsView, MappingsManage, MappingsView,
        UsersManage, RolesManage, ClientUsersManage,
        TicketsViewAll, TicketsViewAssigned, TicketsViewOwnCompany, TicketsViewOwn,
        TicketsCreate, TicketsAddPublicNote, TicketsLogTime, TicketsUpdate,
        ReportsView, ProductivityViewTeam, ProductivityViewOwn,
        IntegrationHealthView, JobsManage, AuditView, SecurityConfigView,
    };

    /// <summary>Default claim set for each built-in role. Used to seed the role table.</summary>
    public static IReadOnlyList<string> ForRole(RoleType role) => role switch
    {
        RoleType.PlatformSuperAdministrator => All,
        RoleType.MspAdministrator => new[]
        {
            OrgManage, ConnectionsManage, ConnectionsView, MappingsManage, MappingsView,
            UsersManage, RolesManage, ClientUsersManage, TicketsViewAll, TicketsCreate,
            TicketsAddPublicNote, TicketsLogTime, TicketsUpdate, ReportsView, ProductivityViewTeam, IntegrationHealthView,
            JobsManage, AuditView, SecurityConfigView,
        },
        RoleType.Manager => new[]
        {
            ConnectionsView, MappingsView, TicketsViewAll, TicketsLogTime, TicketsUpdate, ReportsView,
            ProductivityViewTeam, IntegrationHealthView,
        },
        RoleType.Technician => new[]
        {
            TicketsViewAssigned, TicketsAddPublicNote, TicketsLogTime, TicketsUpdate, ProductivityViewOwn,
        },
        RoleType.ClientAdministrator => new[]
        {
            TicketsViewOwnCompany, TicketsCreate, TicketsAddPublicNote,
            ClientUsersManage, ReportsView,
        },
        RoleType.ClientUser => new[]
        {
            TicketsViewOwn, TicketsCreate, TicketsAddPublicNote,
        },
        RoleType.Auditor => new[]
        {
            AuditView, SecurityConfigView, IntegrationHealthView,
        },
        _ => Array.Empty<string>(),
    };
}
