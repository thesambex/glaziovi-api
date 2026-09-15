namespace Glaziovi.Core.Providers.Identity;

public enum ProvisionUserStatus
{
    Success,
    Conflict,
    BadRequest,
    Failure
}

public sealed record ProvisionUserResult(
    ProvisionUserStatus Status,
    string? UserId
);
