namespace Glaziovi.Modules.Persons;

public enum CreateProfileStatus
{
    Success,
    BadRequest,
    Conflict,
    Failure
}

public sealed record CreateProfileResult(CreateProfileStatus Status, Guid? Id);
