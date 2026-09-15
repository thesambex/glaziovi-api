using Glaziovi.Core.Database;
using Microsoft.EntityFrameworkCore.Storage;

namespace Glaziovi.Database;

internal sealed class Transaction(IDbContextTransaction transaction) : ITransaction
{
    public async Task CommitAsync(CancellationToken ct) =>
        await transaction.CommitAsync(ct);

    public async Task RollbackAsync(CancellationToken ct) =>
        await transaction.RollbackAsync(ct);

    public async ValueTask DisposeAsync() =>
        await transaction.DisposeAsync();
}
