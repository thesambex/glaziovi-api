using Glaziovi.Modules.Iam.Domain;

namespace Glaziovi.Modules.Iam.Repositories;

public interface IUserRepository
{
    Task AddAsync(User user, CancellationToken ct = default);
}
