using Desk.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Desk.Infrastructure.Persistence.Configurations;

public sealed class DepartmentConfig : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.ToTable("departments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.HasIndex(x => new { x.MspOrganizationId, x.Name }).IsUnique();
        b.HasMany(x => x.Teams).WithOne(t => t.Department!).HasForeignKey(t => t.DepartmentId);
    }
}

public sealed class TeamConfig : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> b)
    {
        b.ToTable("teams");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.HasIndex(x => new { x.DepartmentId, x.Name }).IsUnique();
        b.HasOne(x => x.Department).WithMany(d => d.Teams)
            .HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class UserDepartmentConfig : IEntityTypeConfiguration<UserDepartment>
{
    public void Configure(EntityTypeBuilder<UserDepartment> b)
    {
        b.ToTable("user_departments");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.AppUserId, x.DepartmentId }).IsUnique();
        // At most one primary department per user.
        b.HasIndex(x => x.AppUserId).IsUnique().HasFilter("\"IsPrimary\" = true");
        b.HasOne(x => x.Department).WithMany()
            .HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class UserTeamConfig : IEntityTypeConfiguration<UserTeam>
{
    public void Configure(EntityTypeBuilder<UserTeam> b)
    {
        b.ToTable("user_teams");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.AppUserId, x.TeamId }).IsUnique();
        b.HasOne(x => x.Team).WithMany()
            .HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
    }
}
