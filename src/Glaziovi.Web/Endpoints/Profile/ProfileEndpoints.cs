using System.Text.Json.Nodes;
using Glaziovi.Core.Services;
using Glaziovi.Modules.Persons;
using Glaziovi.Web.Endpoints.Profile.Rest;
using Microsoft.OpenApi;

namespace Glaziovi.Web.Endpoints.Profile;

public static class ProfileEndpoints
{
    extension(WebApplication app)
    {
        public void MapProfileEndpoints()
        {
            app.MapPost("api/profiles", async (
                    IProfileService profileService,
                    CreateUserProfileRequest requestBody,
                    CancellationToken ct
                ) =>
                {
                    var createData = new CreateProfile(
                        requestBody.FirstName,
                        requestBody.LastName,
                        requestBody.Email,
                        requestBody.Username,
                        requestBody.Password
                    );

                    var result = await profileService.CreateProfileAsync(createData, ct);
                    return result.Status switch
                    {
                        CreateProfileStatus.Success => Results.Created($"/api/profiles/{result.Id}", null),
                        CreateProfileStatus.BadRequest => Results.Problem(
                            statusCode: StatusCodes.Status400BadRequest),
                        CreateProfileStatus.Conflict => Results.Problem(
                            statusCode: StatusCodes.Status409Conflict),
                        CreateProfileStatus.Failure => Results.Problem(
                            statusCode: StatusCodes.Status500InternalServerError),
                    };
                })
                .WithName("Profiles:Create")
                .AddOpenApiOperationTransformer((operation, _, _) =>
                {
                    operation.Summary = "Create new user profile.";
                    operation.Description = "Creates profile and provision user in identity provider.";
                    operation.Tags = new HashSet<OpenApiTagReference> { new("Profiles") };

                    if (operation.RequestBody?.Content != null &&
                        operation.RequestBody.Content.TryGetValue("application/json", out var mediaType))
                    {
                        mediaType.Example = new JsonObject
                        {
                            ["firstName"] = "John",
                            ["lastName"] = "Doe",
                            ["email"] = "john.doe@example.com",
                            ["username"] = "john",
                            ["password"] = "T$st_123",
                        };
                    }

                    return Task.CompletedTask;
                })
                .ProducesValidationProblem()
                .Produces(StatusCodes.Status201Created)
                .ProducesProblem(StatusCodes.Status409Conflict)
                .Produces(StatusCodes.Status500InternalServerError);
        }
    }
}
