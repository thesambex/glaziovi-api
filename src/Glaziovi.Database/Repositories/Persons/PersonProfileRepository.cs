using Glaziovi.Modules.Persons.Domain;
using Glaziovi.Modules.Persons.Repositories;

namespace Glaziovi.Database.Repositories.Persons;

public sealed class PersonProfileRepository(GlzDbContext dbContext) : IPersonProfileRepository
{
    public async Task AddAsync(PersonProfile profile, CancellationToken ct) =>
        await dbContext.PersonProfiles.AddAsync(profile, ct);
}
