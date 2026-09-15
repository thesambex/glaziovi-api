using Glaziovi.Core.Providers.Identity;

namespace Glaziovi.Core.Providers;

public interface IIdentityProvider
{
    /// <summary>
    /// Provision a new user in external identity provider.
    /// </summary>
    /// <param name="provisionUser">User data input</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<ProvisionUserResult> ProvisionUserAsync(
        IdentityProvisionUser provisionUser,
        CancellationToken ct = default
    );

    /// <summary>
    /// Delete user from external identity provider.
    /// </summary>
    /// <param name="userId">User id</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<bool> DeleteUserAsync(
        string userId,
        CancellationToken ct = default
    );
}
