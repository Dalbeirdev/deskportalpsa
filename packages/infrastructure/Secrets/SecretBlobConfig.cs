using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Desk.Infrastructure.Secrets;

public sealed class SecretBlobConfig : IEntityTypeConfiguration<SecretBlob>
{
    public void Configure(EntityTypeBuilder<SecretBlob> b)
    {
        b.ToTable("secret_blobs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(64).IsRequired();
        b.Property(x => x.LogicalName).HasMaxLength(300).IsRequired();
        b.Property(x => x.Ciphertext).IsRequired();
    }
}
