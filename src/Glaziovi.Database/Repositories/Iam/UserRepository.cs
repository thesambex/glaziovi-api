using Glaziovi.Modules.Iam.Domain;
using Glaziovi.Modules.Iam.Repositories;

namespace Glaziovi.Database.Repositories.Iam;

public sealed class UserRepository(GlzDbContext dbContext) : IUserRepository
{
    public async Task AddAsync(User user, CancellationToken ct) =>
        await dbContext.Users.AddAsync(user, ct);
}
