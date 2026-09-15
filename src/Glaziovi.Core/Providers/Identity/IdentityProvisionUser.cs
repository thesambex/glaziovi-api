namespace Glaziovi.Core.Providers.Identity;

public sealed record IdentityProvisionUser(
    string FirstName,
    string LastName,
    string Email,
    string Username,
    string Password
);
