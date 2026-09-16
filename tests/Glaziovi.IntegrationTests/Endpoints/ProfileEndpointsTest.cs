using System.Net;
using System.Net.Http.Json;
using Glaziovi.Database;
using Glaziovi.IntegrationTests.TestSupport;
using Glaziovi.Web.Endpoints.Profile.Rest;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Glaziovi.IntegrationTests.Endpoints;

public sealed class ProfileEndpointsTest(IntegrationTestSupport fixture)
    : IClassFixture<IntegrationTestSupport>, IAsyncLifetime
{
    private readonly HttpClient _client = fixture.CreateClient();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await fixture.CleanupAsync();
    }

    [Fact]
    public async Task CreateProfile_WithValidRequest_PersistsProfileAndReturnsCreated()
    {
        // Arrange
        var request = CreateRequest();

        // Act
        using var response = await _client.PostAsJsonAsync(
            "/api/profiles",
            request
        );

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var location = Assert.IsType<Uri>(response.Headers.Location);
        var profileId = Guid.Parse(location.OriginalString.Split('/').Last());

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GlzDbContext>();

        var profile = await db.PersonProfiles
            .Include(person => person.User)
            .SingleAsync(person => person.ExternalId == profileId);

        Assert.Equal(request.FirstName, profile.FirstName);
        Assert.Equal(request.LastName, profile.LastName);
        Assert.NotNull(profile.User);
        Assert.NotEmpty(profile.User.ExternalSubject);
    }

    [Fact]
    public async Task CreateProfile_WithDuplicateUsername_ReturnsConflict()
    {
        // Arrange
        var request = CreateRequest();

        using var firstResponse = await _client.PostAsJsonAsync(
            "/api/profiles",
            request
        );

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Act
        using var response = await _client.PostAsJsonAsync(
            "/api/profiles",
            request
        );

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GlzDbContext>();

        Assert.Equal(1, await db.PersonProfiles.CountAsync());
    }

    [Fact]
    public async Task CreateProfile_WithInvalidRequest_ReturnsValidationProblem()
    {
        // Arrange
        var request = CreateRequest() with { Username = ".invalid" };

        // Act
        using var response = await _client.PostAsJsonAsync(
            "/api/profiles",
            request
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GlzDbContext>();

        Assert.Equal(0, await db.PersonProfiles.CountAsync());
    }

    private static CreateUserProfileRequest CreateRequest() => new(
        "John",
        "Doe",
        "john.doe@example.com",
        "john.doe",
        "T$st_123"
    );
}
