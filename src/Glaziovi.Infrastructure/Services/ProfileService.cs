using Glaziovi.Core.Database;
using Glaziovi.Core.Providers;
using Glaziovi.Core.Providers.Identity;
using Glaziovi.Core.Services;
using Glaziovi.Modules.Iam.Domain;
using Glaziovi.Modules.Iam.Repositories;
using Glaziovi.Modules.Persons;
using Glaziovi.Modules.Persons.Domain;
using Glaziovi.Modules.Persons.Repositories;
using Microsoft.Extensions.Logging;

namespace Glaziovi.Infrastructure.Services;

public sealed class ProfileService(
    IUserRepository userRepository,
    IPersonProfileRepository personProfileRepository,
    IUnitOfWork unitOfWork,
    IIdentityProvider identityProvider,
    ILogger<ProfileService> logger
) : IProfileService
{
    public async Task<CreateProfileResult> CreateProfileAsync(
        CreateProfile createData,
        CancellationToken ct
    )
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var provisionUser = new IdentityProvisionUser(
                createData.FirstName,
                createData.LastName,
                createData.Email,
                createData.Username,
                createData.Password
            );

            var provisionResult = await identityProvider.ProvisionUserAsync(provisionUser, ct);
            if (provisionResult.Status != ProvisionUserStatus.Success)
            {
                await transaction.RollbackAsync(ct);

                return provisionResult.Status switch
                {
                    ProvisionUserStatus.Conflict => new CreateProfileResult(CreateProfileStatus.Conflict, null),
                    ProvisionUserStatus.BadRequest => new CreateProfileResult(CreateProfileStatus.BadRequest, null),
                    ProvisionUserStatus.Failure => new CreateProfileResult(CreateProfileStatus.Failure, null),
                    _ => throw new ArgumentException("Unknown keycloak error")
                };
            }

            var user = new User(provisionResult.UserId!);
            await userRepository.AddAsync(user, ct);

            var personProfile = new PersonProfile(
                user.Id,
                createData.FirstName,
                createData.LastName,
                null
            );

            await personProfileRepository.AddAsync(personProfile, ct);

            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            logger.LogInformation("[Profile] Profile {id} created with success", personProfile.ExternalId);

            return new CreateProfileResult(CreateProfileStatus.Success, personProfile.ExternalId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);

            logger.LogError(ex, "[Profile] Failed to create profile");

            return new CreateProfileResult(CreateProfileStatus.Failure, null);
        }
    }
}
