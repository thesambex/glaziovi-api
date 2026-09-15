using Glaziovi.Core.Database;

namespace Glaziovi.Database;

public sealed class UnitOfWork(GlzDbContext dbContext) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken ct) =>
        await dbContext.SaveChangesAsync(ct);

    public async Task<ITransaction> BeginTransactionAsync(CancellationToken ct) =>
        new Transaction(await dbContext.Database.BeginTransactionAsync(ct));
}
