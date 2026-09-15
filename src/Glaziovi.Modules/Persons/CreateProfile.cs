namespace Glaziovi.Modules.Persons;

public sealed record CreateProfile(
    string FirstName,
    string LastName,
    string Email,
    string Username,
    string Password
);
