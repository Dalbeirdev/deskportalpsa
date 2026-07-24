using Desk.Domain.Audit;
using Desk.Domain.Identity;
using Desk.Domain.Mapping;
using Desk.Domain.Sync;
using Desk.Domain.Tenancy;
using Desk.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Desk.Infrastructure.Persistence.Configurations;

public sealed class MspOrganizationConfig : IEntityTypeConfiguration<MspOrganization>
{
    public void Configure(EntityTypeBuilder<MspOrganization> b)
    {
        b.ToTable("msp_organizations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
    }
}

public sealed class PsaConnectionConfig : IEntityTypeConfiguration<PsaConnection>
{
    public void Configure(EntityTypeBuilder<PsaConnection> b)
    {
        b.ToTable("psa_connections");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.ApiEndpoint).HasMaxLength(500).IsRequired();
        b.Property(x => x.CredentialSecretRef).HasMaxLength(500).IsRequired();
        b.HasIndex(x => x.MspOrganizationId);
        b.HasOne<MspOrganization>().WithMany(o => o.PsaConnections)
            .HasForeignKey(x => x.MspOrganizationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ClientCompanyConfig : IEntityTypeConfiguration<ClientCompany>
{
    public void Configure(EntityTypeBuilder<ClientCompany> b)
    {
        b.ToTable("client_companies");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.ExternalCompanyId).HasMaxLength(200).IsRequired();
        // One external company maps once per connection.
        b.HasIndex(x => new { x.PsaConnectionId, x.ExternalCompanyId }).IsUnique();
        b.HasOne(x => x.PsaConnection).WithMany(c => c.ClientCompanies)
            .HasForeignKey(x => x.PsaConnectionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ClientUserConfig : IEntityTypeConfiguration<ClientUser>
{
    public void Configure(EntityTypeBuilder<ClientUser> b)
    {
        b.ToTable("client_users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Email).HasMaxLength(320).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        b.HasIndex(x => new { x.ClientCompanyId, x.Email }).IsUnique();
        b.HasIndex(x => x.IdpSubject);
        b.HasOne(x => x.ClientCompany).WithMany(c => c.Users)
            .HasForeignKey(x => x.ClientCompanyId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AppUserConfig : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b)
    {
        b.ToTable("app_users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Email).HasMaxLength(320).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.IdpSubject).IsUnique();
        b.HasIndex(x => new { x.MspOrganizationId, x.Email });
        b.HasMany(x => x.Roles).WithOne(r => r.AppUser!).HasForeignKey(r => r.AppUserId);
    }
}

public sealed class RoleConfig : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("roles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.HasMany(x => x.Permissions).WithOne(p => p.Role!).HasForeignKey(p => p.RoleId);
        b.HasIndex(x => new { x.MspOrganizationId, x.Name });
    }
}

public sealed class RolePermissionConfig : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.ToTable("role_permissions");
        b.HasKey(x => x.Id);
        b.Property(x => x.PermissionKey).HasMaxLength(100).IsRequired();
        b.HasIndex(x => new { x.RoleId, x.PermissionKey }).IsUnique();
    }
}

public sealed class UserRoleConfig : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> b)
    {
        b.ToTable("user_roles");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.AppUserId, x.RoleId }).IsUnique();
    }
}

public sealed class TicketConfig : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> b)
    {
        b.ToTable("tickets");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(500).IsRequired();
        b.Property(x => x.RequesterName).HasMaxLength(200).IsRequired();
        b.Property(x => x.RequesterEmail).HasMaxLength(320).IsRequired();
        b.Property(x => x.TimeWorkedHours).HasPrecision(10, 2);
        b.Property(x => x.BillableHours).HasPrecision(10, 2);
        b.Property(x => x.NonBillableHours).HasPrecision(10, 2);
        b.Property(x => x.Version).IsConcurrencyToken();
        // A given PSA ticket appears once per connection.
        b.HasIndex(x => new { x.PsaConnectionId, x.ExternalTicketId })
            .IsUnique()
            .HasFilter("\"ExternalTicketId\" IS NOT NULL");
        b.HasIndex(x => new { x.MspOrganizationId, x.PortalStatus });
        b.HasIndex(x => x.CorrelationId);
        b.HasMany(x => x.Notes).WithOne(n => n.Ticket!).HasForeignKey(n => n.TicketId);
        b.HasMany(x => x.Attachments).WithOne(a => a.Ticket!).HasForeignKey(a => a.TicketId);
    }
}

public sealed class TicketNoteConfig : IEntityTypeConfiguration<TicketNote>
{
    public void Configure(EntityTypeBuilder<TicketNote> b)
    {
        b.ToTable("ticket_notes");
        b.HasKey(x => x.Id);
        b.Property(x => x.AuthorName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Body).IsRequired();
        b.HasIndex(x => x.TicketId);
    }
}

public sealed class TicketAttachmentConfig : IEntityTypeConfiguration<TicketAttachment>
{
    public void Configure(EntityTypeBuilder<TicketAttachment> b)
    {
        b.ToTable("ticket_attachments");
        b.HasKey(x => x.Id);
        b.Property(x => x.OriginalFileName).HasMaxLength(400).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(200).IsRequired();
        b.Property(x => x.StorageObjectKey).HasMaxLength(400).IsRequired();
        b.HasIndex(x => x.TicketId);
    }
}

public sealed class FieldMappingConfig : IEntityTypeConfiguration<FieldMapping>
{
    public void Configure(EntityTypeBuilder<FieldMapping> b)
    {
        b.ToTable("field_mappings");
        b.HasKey(x => x.Id);
        b.Property(x => x.PortalField).HasMaxLength(100).IsRequired();
        b.Property(x => x.ExternalField).HasMaxLength(100).IsRequired();
        b.HasIndex(x => new { x.MspOrganizationId, x.Provider, x.Scope });
    }
}

public sealed class FieldMappingVersionConfig : IEntityTypeConfiguration<FieldMappingVersion>
{
    public void Configure(EntityTypeBuilder<FieldMappingVersion> b)
    {
        b.ToTable("field_mapping_versions");
        b.HasKey(x => x.Id);
        b.Property(x => x.SnapshotJson).IsRequired();
        b.HasIndex(x => new { x.MspOrganizationId, x.Provider, x.PsaConnectionId, x.Version });
    }
}

public sealed class SyncEventConfig : IEntityTypeConfiguration<SyncEvent>
{
    public void Configure(EntityTypeBuilder<SyncEvent> b)
    {
        b.ToTable("sync_events");
        b.HasKey(x => x.Id);
        b.Property(x => x.EventType).HasMaxLength(100).IsRequired();
        b.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired();
        b.Property(x => x.SourceMarker).HasMaxLength(20).IsRequired();
        // Duplicate deliveries of the same source event are dropped by this constraint.
        b.HasIndex(x => new { x.PsaConnectionId, x.IdempotencyKey }).IsUnique();
        b.HasIndex(x => x.TicketId);
    }
}

public sealed class BackgroundJobConfig : IEntityTypeConfiguration<BackgroundJob>
{
    public void Configure(EntityTypeBuilder<BackgroundJob> b)
    {
        b.ToTable("background_jobs");
        b.HasKey(x => x.Id);
        b.Property(x => x.JobType).HasMaxLength(100).IsRequired();
        b.Property(x => x.PayloadJson).IsRequired();
        b.HasIndex(x => new { x.Status, x.NextAttemptAt });
    }
}

public sealed class AuditLogEntryConfig : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> b)
    {
        b.ToTable("audit_log");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasMaxLength(100).IsRequired();
        b.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        b.HasIndex(x => new { x.MspOrganizationId, x.CreatedAt });
        b.HasIndex(x => x.CorrelationId);
    }
}
