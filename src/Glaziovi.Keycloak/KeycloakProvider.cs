using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Glaziovi.Core.Providers;
using Glaziovi.Core.Providers.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Glaziovi.Keycloak;

public sealed class KeycloakProvider(
    HttpClient httpClient,
    IOptions<KeycloakOptions> keycloakOptions,
    ILogger<KeycloakProvider> logger
) : IIdentityProvider
{
    private readonly KeycloakOptions _options = keycloakOptions.Value;

    private static readonly Dictionary<IdentityRole, string> s_userRoleMap = new() { { IdentityRole.User, "user" }, };


    public async Task<ProvisionUserResult> ProvisionUserAsync(
        IdentityProvisionUser provisionUser,
        CancellationToken ct
    )
    {
        var createUserRequest = new Dictionary<string, object?>
        {
            ["username"] = provisionUser.Username,
            ["email"] = provisionUser.Email,
            ["firstName"] = provisionUser.FirstName,
            ["lastName"] = provisionUser.LastName,
            ["enabled"] = true,
            ["emailVerified"] = false,
            ["credentials"] = new[] { new { type = "password", value = provisionUser.Password, temporary = false } }
        };

        using var createUserResponse = await SendAdminRequestAsync(
            HttpMethod.Post, "users", ct, JsonContent.Create(createUserRequest)
        );

        if (!createUserResponse.IsSuccessStatusCode)
        {
            return createUserResponse.StatusCode switch
            {
                HttpStatusCode.BadRequest => new ProvisionUserResult(ProvisionUserStatus.BadRequest, null),
                HttpStatusCode.Conflict => new ProvisionUserResult(ProvisionUserStatus.Conflict, null),
                HttpStatusCode.InternalServerError => new ProvisionUserResult(ProvisionUserStatus.Failure, null),

                _ => throw new InvalidOperationException("[Keycloak] Unknown error on keycloak user creation"),
            };
        }

        var location = createUserResponse.Headers.Location;
        string userId = location!.Segments.Last();

        var roles = new HashSet<IdentityRole> { IdentityRole.User };

        if (!await AttachRolesAsync(userId, roles, ct))
        {
            if (!await DeleteUserAsync(userId, ct))
            {
                logger.LogWarning(
                    "[Keycloak] Falha ao deletar o usuário {userId} durante a falha de anexo de roles durante o provisionamento",
                    userId);
            }

            return new ProvisionUserResult(ProvisionUserStatus.Failure, null);
        }

        logger.LogInformation("[Keycloak] Usuário {userId} provisionado com sucesso", userId);

        return new ProvisionUserResult(ProvisionUserStatus.Success, userId);
    }

    private async Task<bool> AttachRolesAsync(
        string userId,
        ISet<IdentityRole> userRoles,
        CancellationToken ct = default
    )
    {
        using var rolesResponse = await SendAdminRequestAsync(HttpMethod.Get, "roles", ct);
        rolesResponse.EnsureSuccessStatusCode();
        var roles = await rolesResponse.Content.ReadFromJsonAsync<List<KeycloakRoleData>>(ct);

        var desiredRoleNames = userRoles
            .Select(x => s_userRoleMap[x])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rolesToAssign = roles!
            .Where(x => desiredRoleNames.Contains(x.Name))
            .Select(x => new { id = x.Id, name = x.Name })
            .ToArray();

        using var attachRolesResponse = await SendAdminRequestAsync(
            HttpMethod.Post, $"users/{Uri.EscapeDataString(userId)}/role-mappings/realm",
            ct, JsonContent.Create(rolesToAssign)
        );

        return attachRolesResponse.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteUserAsync(
        string userId,
        CancellationToken ct
    )
    {
        using var response = await SendAdminRequestAsync(
            HttpMethod.Delete, $"users/{Uri.EscapeDataString(userId)}", ct
        );

        return response.IsSuccessStatusCode;
    }

    private async Task<HttpResponseMessage> SendAdminRequestAsync(
        HttpMethod method,
        string path,
        CancellationToken ct,
        HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method,
            $"/admin/realms/{Uri.EscapeDataString(_options.Realm)}/{path}");

        request.Content = content;
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(ct));

        return await httpClient.SendAsync(request, ct);
    }

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret
        });

        using var response = await httpClient.PostAsync(
            $"/realms/{_options.Realm}/protocol/openid-connect/token",
            content, ct
        );

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        return json.GetProperty("access_token").GetString()!;
    }
}
