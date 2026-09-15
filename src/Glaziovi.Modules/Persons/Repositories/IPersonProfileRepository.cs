using Glaziovi.Modules.Persons.Domain;

namespace Glaziovi.Modules.Persons.Repositories;

public interface IPersonProfileRepository
{
    Task AddAsync(PersonProfile profile, CancellationToken ct = default);
}
