using Desk.Domain.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Desk.Infrastructure.Persistence.Configurations;

public sealed class UserBoardAccessConfig : IEntityTypeConfiguration<UserBoardAccess>
{
    public void Configure(EntityTypeBuilder<UserBoardAccess> b)
    {
        b.ToTable("user_board_access");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.AppUserId).IsUnique();
    }
}

public sealed class UserBoardGrantConfig : IEntityTypeConfiguration<UserBoardGrant>
{
    public void Configure(EntityTypeBuilder<UserBoardGrant> b)
    {
        b.ToTable("user_board_grants");
        b.HasKey(x => x.Id);
        b.Property(x => x.BoardName).HasMaxLength(300).IsRequired();
        b.HasIndex(x => new { x.AppUserId, x.PsaConnectionId, x.BoardName }).IsUnique();
        b.HasOne(x => x.PsaConnection).WithMany()
            .HasForeignKey(x => x.PsaConnectionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class UserPermissionOverrideConfig : IEntityTypeConfiguration<UserPermissionOverride>
{
    public void Configure(EntityTypeBuilder<UserPermissionOverride> b)
    {
        b.ToTable("user_permission_overrides");
        b.HasKey(x => x.Id);
        b.Property(x => x.PermissionKey).HasMaxLength(100).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(500);
        b.HasIndex(x => new { x.AppUserId, x.PermissionKey }).IsUnique();
    }
}

public sealed class PermissionTemplateConfig : IEntityTypeConfiguration<PermissionTemplate>
{
    public void Configure(EntityTypeBuilder<PermissionTemplate> b)
    {
        b.ToTable("permission_templates");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.HasIndex(x => new { x.MspOrganizationId, x.Name }).IsUnique();
        b.HasMany(x => x.Entries).WithOne(e => e.PermissionTemplate!).HasForeignKey(e => e.PermissionTemplateId);
    }
}

public sealed class PermissionTemplateEntryConfig : IEntityTypeConfiguration<PermissionTemplateEntry>
{
    public void Configure(EntityTypeBuilder<PermissionTemplateEntry> b)
    {
        b.ToTable("permission_template_entries");
        b.HasKey(x => x.Id);
        b.Property(x => x.PermissionKey).HasMaxLength(100).IsRequired();
        b.HasIndex(x => new { x.PermissionTemplateId, x.PermissionKey }).IsUnique();
        b.HasOne(x => x.PermissionTemplate).WithMany(t => t.Entries)
            .HasForeignKey(x => x.PermissionTemplateId).OnDelete(DeleteBehavior.Cascade);
    }
}
