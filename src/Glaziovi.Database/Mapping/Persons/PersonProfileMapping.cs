using Glaziovi.Modules.Persons.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glaziovi.Database.Mapping.Persons;

internal class PersonProfileMapping : IEntityTypeConfiguration<PersonProfile>
{
    public void Configure(EntityTypeBuilder<PersonProfile> builder)
    {
        builder.ToTable("person_profiles", "persons");

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd()
            .IsRequired();

        builder.Property(e => e.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(e => e.LastName)
            .HasColumnName("last_name")
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(e => e.BirthDate)
            .HasColumnName("birth_date")
            .HasColumnType("date");

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(e => e.ExternalId)
            .HasColumnName("external_id")
            .HasDefaultValueSql("uuidv7()")
            .IsRequired();


        builder.HasIndex(e => e.ExternalId)
            .HasDatabaseName("IX_person_profiles_external_id");

        builder.HasOne(e => e.User)
            .WithOne(e => e.Profile)
            .HasForeignKey<PersonProfile>(e => e.UserId)
            .HasConstraintName("FK_person_profiles_users")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
