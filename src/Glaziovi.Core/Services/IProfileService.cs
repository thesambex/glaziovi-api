using Glaziovi.Modules.Persons;

namespace Glaziovi.Core.Services;

public interface IProfileService
{
    /// <summary>
    /// Create a new user profile
    /// </summary>
    /// <param name="createData">Create profile input</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<CreateProfileResult> CreateProfileAsync(
        CreateProfile createData,
        CancellationToken ct = default
    );
}
