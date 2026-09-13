using Glaziovi.Modules.Iam.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glaziovi.Database.Mapping.Users;

internal class UserMapping : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("iam_users", "iam");

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd()
            .IsRequired();

        builder.Property(e => e.ExternalSubject)
            .HasColumnName("external_subject")
            .HasMaxLength(255)
            .IsRequired();


        builder.HasIndex(e => e.ExternalSubject)
            .IsUnique()
            .HasDatabaseName("UX_iam_users_external_subject");
    }
}
