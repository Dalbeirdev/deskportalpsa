using System.Linq.Expressions;
using Desk.Application.Abstractions;
using Desk.Domain.Audit;
using Desk.Domain.Common;
using Desk.Domain.ControlPanel;
using Desk.Domain.Identity;
using Desk.Domain.Mapping;
using Desk.Domain.Sync;
using Desk.Domain.Tenancy;
using Desk.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace Desk.Infrastructure.Persistence;

/// <summary>
/// The application database context. Two isolation guarantees are enforced here and cannot be
/// bypassed by callers:
///   1. READ — a global query filter constrains every <see cref="ITenantScoped"/> entity to the
///      current tenant (except for platform scope). No repository can opt out.
///   2. WRITE — on save, new tenant-scoped rows are stamped with the current tenant, and any
///      attempt to insert/modify a row for a different tenant is rejected.
/// The audit log is append-only: modifying or deleting an existing entry throws.
/// </summary>
public class DeskDbContext(DbContextOptions<DeskDbContext> options, ITenantContext tenant, TimeProvider clock)
    : DbContext(options)
{
    public DbSet<MspOrganization> MspOrganizations => Set<MspOrganization>();
    public DbSet<PsaConnection> PsaConnections => Set<PsaConnection>();
    public DbSet<ClientCompany> ClientCompanies => Set<ClientCompany>();
    public DbSet<ClientUser> ClientUsers => Set<ClientUser>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketNote> TicketNotes => Set<TicketNote>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
    public DbSet<FieldMapping> FieldMappings => Set<FieldMapping>();
    public DbSet<FieldMappingVersion> FieldMappingVersions => Set<FieldMappingVersion>();
    public DbSet<SyncEvent> SyncEvents => Set<SyncEvent>();
    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();
    public DbSet<TicketInstruction> TicketInstructions => Set<TicketInstruction>();
    public DbSet<ClientAccessGrant> ClientAccessGrants => Set<ClientAccessGrant>();

    // Read by the compiled query filter below. Guid.Empty can never match a real row, so an
    // unresolved (null) tenant that is not platform scope yields zero rows — fail closed.
    private Guid CurrentTenantId => tenant.OrganizationId ?? Guid.Empty;
    private bool BypassTenantFilter => tenant.IsPlatformScope;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DeskDbContext).Assembly);

        // SQLite (local mode) can't ORDER BY or range-compare DateTimeOffset. Store every timestamp
        // as a sortable binary long so all timestamp queries translate. No effect on Postgres.
        if (Database.IsSqlite())
        {
            var converter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.DateTimeOffsetToBinaryConverter();
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
                foreach (var prop in entityType.GetProperties())
                    if (prop.ClrType == typeof(DateTimeOffset) || prop.ClrType == typeof(DateTimeOffset?))
                        prop.SetValueConverter(converter);
        }

        // Apply the tenant query filter to every entity implementing ITenantScoped.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(DeskDbContext)
                    .GetMethod(nameof(BuildTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(entityType.ClrType);
                var filter = method.Invoke(this, null)!;
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter((LambdaExpression)filter);
            }
        }
    }

    private LambdaExpression BuildTenantFilter<TEntity>() where TEntity : class, ITenantScoped
        // References instance members, so EF re-evaluates per DbContext instance/query.
        => (Expression<Func<TEntity, bool>>)(e => BypassTenantFilter || e.MspOrganizationId == CurrentTenantId);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyInvariants();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        ApplyInvariants();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
    }

    private void ApplyInvariants()
    {
        var now = clock.GetUtcNow();

        foreach (var entry in ChangeTracker.Entries())
        {
            // Timestamps
            if (entry is { Entity: BaseEntity be, State: EntityState.Added })
            {
                if (be.CreatedAt == default) be.CreatedAt = now;
                be.UpdatedAt = now;
            }
            else if (entry is { Entity: BaseEntity ue, State: EntityState.Modified })
            {
                ue.UpdatedAt = now;
            }

            // Audit log is append-only.
            if (entry.Entity is AuditLogEntry && entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("Audit log entries are immutable and cannot be modified or deleted.");

            // Tenant write isolation.
            if (entry.Entity is ITenantScoped scoped)
            {
                if (entry.State == EntityState.Added)
                {
                    // Stamp new rows with the active tenant unless running under platform scope.
                    if (!BypassTenantFilter)
                    {
                        if (!tenant.OrganizationId.HasValue)
                            throw new InvalidOperationException("Cannot persist a tenant-scoped entity without an established tenant scope.");
                        if (scoped.MspOrganizationId == Guid.Empty)
                            scoped.MspOrganizationId = tenant.OrganizationId.Value;
                        else if (scoped.MspOrganizationId != tenant.OrganizationId.Value)
                            throw new InvalidOperationException("Cross-tenant write blocked: entity tenant does not match the current scope.");
                    }
                }
                else if (entry.State is EntityState.Modified or EntityState.Deleted && !BypassTenantFilter)
                {
                    if (tenant.OrganizationId.HasValue && scoped.MspOrganizationId != tenant.OrganizationId.Value)
                        throw new InvalidOperationException("Cross-tenant write blocked: entity belongs to a different tenant.");
                }
            }
        }
    }
}
