using Glaziovi.Modules.Iam.Domain;
using Glaziovi.Modules.Persons.Domain;
using Microsoft.EntityFrameworkCore;

namespace Glaziovi.Database;

public class GlzDbContext(DbContextOptions<GlzDbContext> options) : DbContext(options)
{
    #region Entities

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<PersonProfile> PersonProfiles { get; set; }

    #endregion

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasPostgresExtension("uuid-ossp");
        builder.HasPostgresExtension("postgis");

        builder.UseIdentityAlwaysColumns();

        builder.ApplyConfigurationsFromAssembly(typeof(GlzDbContext).Assembly);
    }
}
