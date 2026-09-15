using Glaziovi.Modules.Persons.Domain;

namespace Glaziovi.Modules.Iam.Domain;

/// <summary>
/// Represents a external user linked to external identity provider
/// </summary>
/// <param name="externalSubject">External identity provider Id</param>
public sealed class User(string externalSubject)
{
    public long Id { get; private init; }
    public string ExternalSubject { get; } = externalSubject;

    public PersonProfile? Profile { get; set; }
}
