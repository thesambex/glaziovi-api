using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Glaziovi.Core.Providers.Identity;
using Glaziovi.Keycloak;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Glaziovi.UnitTests.Providers;

public sealed class KeycloakProviderTest
{
    [Theory]
    [InlineData("user")]
    [InlineData("USER")]
    public async Task ProvisionUserAsync_WhenRequestsSucceed_CreatesUserWithDefaultRole(
        string roleName
    )
    {
        // Arrange
        using var handler = new KeycloakHttpHandler(
            CreateUserResponse(),
            RolesResponse(roleName),
            new HttpResponseMessage(HttpStatusCode.NoContent)
        );
        using var client = CreateClient(handler);
        var provider = CreateProvider(client);
        var request = CreateRequest();

        // Act
        var result = await provider.ProvisionUserAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(ProvisionUserStatus.Success, result.Status);
        Assert.Equal("created-user", result.UserId);
        Assert.Collection(
            handler.AdminRequests,
            create =>
            {
                Assert.Equal(HttpMethod.Post, create.Method);
                Assert.Equal("/admin/realms/test-realm/users", create.Path);
                var body = JsonSerializer.Deserialize<JsonElement>(create.Body!);
                Assert.Equal(request.Username, body.GetProperty("username").GetString());
                Assert.Equal(request.Email, body.GetProperty("email").GetString());
                Assert.Equal(request.FirstName, body.GetProperty("firstName").GetString());
                Assert.Equal(request.LastName, body.GetProperty("lastName").GetString());
                Assert.True(body.GetProperty("enabled").GetBoolean());
                Assert.False(body.GetProperty("emailVerified").GetBoolean());
                var credential = Assert.Single(body.GetProperty("credentials").EnumerateArray());
                Assert.Equal("password", credential.GetProperty("type").GetString());
                Assert.Equal(request.Password, credential.GetProperty("value").GetString());
                Assert.False(credential.GetProperty("temporary").GetBoolean());
            },
            roles =>
            {
                Assert.Equal(HttpMethod.Get, roles.Method);
                Assert.Equal("/admin/realms/test-realm/roles", roles.Path);
            },
            attach =>
            {
                Assert.Equal(HttpMethod.Post, attach.Method);
                Assert.Equal(
                    "/admin/realms/test-realm/users/created-user/role-mappings/realm",
                    attach.Path
                );
                var body = JsonSerializer.Deserialize<JsonElement>(attach.Body!);
                var role = Assert.Single(body.EnumerateArray());
                Assert.Equal("user-role-id", role.GetProperty("id").GetString());
                Assert.Equal(roleName, role.GetProperty("name").GetString());
            }
        );
        Assert.All(handler.AdminRequests, request => Assert.Equal("Bearer test-token", request.Authorization));
        Assert.NotEmpty(handler.TokenRequests);
        Assert.All(handler.TokenRequests, token =>
        {
            Assert.Equal(HttpMethod.Post, token.Method);
            Assert.Equal("/realms/test-realm/protocol/openid-connect/token", token.Path);
            Assert.Equal(
                "grant_type=client_credentials&client_id=test-client&client_secret=test-secret",
                token.Body
            );
        });
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, ProvisionUserStatus.BadRequest)]
    [InlineData(HttpStatusCode.Conflict, ProvisionUserStatus.Conflict)]
    [InlineData(HttpStatusCode.InternalServerError, ProvisionUserStatus.Failure)]
    public async Task ProvisionUserAsync_WhenCreationIsRejected_ReturnsMappedStatus(
        HttpStatusCode statusCode,
        ProvisionUserStatus expectedStatus
    )
    {
        // Arrange
        using var handler = new KeycloakHttpHandler(new HttpResponseMessage(statusCode));
        using var client = CreateClient(handler);
        var provider = CreateProvider(client);
        var request = CreateRequest();

        // Act
        var result = await provider.ProvisionUserAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(expectedStatus, result.Status);
        Assert.Null(result.UserId);
        Assert.Single(handler.AdminRequests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task ProvisionUserAsync_WhenCreationReturnsUnexpectedStatus_Throws(
        HttpStatusCode statusCode
    )
    {
        // Arrange
        using var handler = new KeycloakHttpHandler(new HttpResponseMessage(statusCode));
        using var client = CreateClient(handler);
        var provider = CreateProvider(client);
        var request = CreateRequest();

        // Act
        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                provider.ProvisionUserAsync(request, CancellationToken.None)
            );

        // Assert
        Assert.Contains("user creation", exception.Message);
        Assert.Single(handler.AdminRequests);
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ProvisionUserAsync_WhenRoleAssignmentFails_AttemptsDeletionAndReturnsFailure(
        HttpStatusCode deletionStatus
    )
    {
        // Arrange
        using var handler = new KeycloakHttpHandler(
            CreateUserResponse(),
            RolesResponse("user"),
            new HttpResponseMessage(HttpStatusCode.BadRequest),
            new HttpResponseMessage(deletionStatus)
        );
        using var client = CreateClient(handler);
        var provider = CreateProvider(client);
        var request = CreateRequest();

        // Act
        var result = await provider.ProvisionUserAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(ProvisionUserStatus.Failure, result.Status);
        Assert.Null(result.UserId);
        Assert.Equal(4, handler.AdminRequests.Count);
        var deletion = handler.AdminRequests.Last();
        Assert.Equal(HttpMethod.Delete, deletion.Method);
        Assert.Equal("/admin/realms/test-realm/users/created-user", deletion.Path);
    }

    [Fact]
    public async Task ProvisionUserAsync_WhenRoleLookupFails_PropagatesHttpException()
    {
        // Arrange
        using var handler = new KeycloakHttpHandler(
            CreateUserResponse(),
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        );
        using var client = CreateClient(handler);
        var provider = CreateProvider(client);
        var request = CreateRequest();

        // Act
        var exception =
            await Assert.ThrowsAsync<HttpRequestException>(() =>
                provider.ProvisionUserAsync(request, CancellationToken.None)
            );

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        Assert.Equal(2, handler.AdminRequests.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.NoContent, true)]
    [InlineData(HttpStatusCode.NotFound, false)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    public async Task DeleteUserAsync_WhenResponseReceived_ReturnsWhetherSuccessful(
        HttpStatusCode statusCode,
        bool expectedResult
    )
    {
        // Arrange
        using var handler = new KeycloakHttpHandler(new HttpResponseMessage(statusCode));
        using var client = CreateClient(handler);
        var provider = CreateProvider(client);

        // Act
        var result = await provider.DeleteUserAsync("user with space", CancellationToken.None);

        // Assert
        Assert.Equal(expectedResult, result);
        var request = Assert.Single(handler.AdminRequests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/admin/realms/test-realm/users/user%20with%20space", request.Path);
        Assert.Equal("Bearer test-token", request.Authorization);
        Assert.Null(request.Body);
    }

    [Fact]
    public async Task DeleteUserAsync_WhenAuthenticationFails_DoesNotSendAdminRequest()
    {
        // Arrange
        using var handler = new KeycloakHttpHandler { TokenStatusCode = HttpStatusCode.Unauthorized };
        using var client = CreateClient(handler);
        var provider = CreateProvider(client);

        // Act
        var exception =
            await Assert.ThrowsAsync<HttpRequestException>(() =>
                provider.DeleteUserAsync("created-user", CancellationToken.None)
            );

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Single(handler.TokenRequests);
        Assert.Empty(handler.AdminRequests);
    }

    [Fact]
    public async Task DeleteUserAsync_WhenCanceled_PropagatesCancellation()
    {
        // Arrange
        using var handler = new KeycloakHttpHandler();
        using var client = CreateClient(handler);
        var provider = CreateProvider(client);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.DeleteUserAsync("created-user", cancellation.Token)
        );

        // Assert
        Assert.Empty(handler.AdminRequests);
    }

    private static HttpClient CreateClient(HttpMessageHandler handler) => new(handler)
    {
        BaseAddress = new Uri("https://keycloak.example")
    };

    private static KeycloakProvider CreateProvider(HttpClient client) => new(
        client,
        Options.Create(new KeycloakOptions
        {
            Realm = "test-realm", ClientId = "test-client", ClientSecret = "test-secret"
        }),
        NullLogger<KeycloakProvider>.Instance
    );

    private static IdentityProvisionUser CreateRequest() => new(
        "Ana",
        "Silva",
        "ana@example.com",
        "ana.silva",
        "test-password"
    );

    private static HttpResponseMessage CreateUserResponse() => new(HttpStatusCode.Created)
    {
        Headers = { Location = new Uri("https://keycloak.example/admin/realms/test-realm/users/created-user") }
    };

    private static HttpResponseMessage RolesResponse(string roleName) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new[]
        {
            new KeycloakRoleData("admin-role-id", "admin"), new KeycloakRoleData("user-role-id", roleName)
        })
    };

    private sealed record RecordedRequest(
        HttpMethod Method,
        string Path,
        string? Authorization,
        string? Body
    );

    private sealed class KeycloakHttpHandler(
        params HttpResponseMessage[] responses
    ) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<RecordedRequest> AdminRequests { get; } = [];
        public List<RecordedRequest> TokenRequests { get; } = [];
        public HttpStatusCode TokenStatusCode { get; init; } = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            var recorded = new RecordedRequest(
                request.Method,
                request.RequestUri!.AbsolutePath,
                request.Headers.Authorization?.ToString(),
                request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync(cancellationToken)
            );

            if (recorded.Path.EndsWith("/protocol/openid-connect/token", StringComparison.Ordinal))
            {
                TokenRequests.Add(recorded);

                return new HttpResponseMessage(TokenStatusCode)
                {
                    Content = JsonContent.Create(new { access_token = "test-token" })
                };
            }

            AdminRequests.Add(recorded);

            if (!_responses.TryDequeue(out var response))
            {
                throw new InvalidOperationException($"Unexpected request: {recorded.Method} {recorded.Path}");
            }

            return response;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var response in _responses)
                {
                    response.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}
