using Glaziovi.Modules.Iam.Domain;

namespace Glaziovi.Modules.Persons.Domain;

/// <summary>
/// Represents a person profile in platform.
/// </summary>
/// <param name="userId">User Id</param>
/// <param name="firstName">First Name</param>
/// <param name="lastName">Last Name</param>
/// <param name="birthDate">Birth Date</param>
public sealed class PersonProfile(
    long userId,
    string firstName,
    string lastName,
    DateOnly? birthDate
)
{
    public long Id { get; private init; }
    public long UserId => userId;
    public string FirstName { get; private set; } = firstName;
    public string LastName { get; private set; } = lastName;
    public DateOnly? BirthDate { get; private set; } = birthDate;
    public Guid ExternalId { get; } = Guid.CreateVersion7();

    public User? User { get; set; }
}
