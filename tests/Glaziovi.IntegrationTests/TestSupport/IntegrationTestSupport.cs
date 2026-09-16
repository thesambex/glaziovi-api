using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Glaziovi.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.Keycloak;
using Testcontainers.PostgreSql;

namespace Glaziovi.IntegrationTests.TestSupport;

public sealed class IntegrationTestSupport : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgis/postgis:18-3.6-alpine")
            .WithDatabase("postgres")
            .WithUsername("postgres")
            .WithPassword("integration-postgres")
            .Build();

    private readonly KeycloakContainer _keycloak =
        new KeycloakBuilder("quay.io/keycloak/keycloak:26.7.3")
            .WithUsername("admin")
            .WithPassword("integration-admin")
            .WithRealm(Path.Combine(
                AppContext.BaseDirectory,
                "TestSupport",
                "realm.json"
            ))
            .Build();

    private readonly string _databaseName = $"glaziovi_{Guid.NewGuid():N}";
    private string _connectionString = null!;

    public async ValueTask InitializeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var ct = timeout.Token;

        try
        {
            await Task.WhenAll(
                _postgres.StartAsync(ct),
                _keycloak.StartAsync(ct)
            );

            _connectionString = new NpgsqlConnectionStringBuilder(
                _postgres.GetConnectionString()
            )
            {
                Database = _databaseName,
                Pooling = false
            }.ConnectionString;

            await ExecuteAdminSqlAsync($"CREATE DATABASE {_databaseName}", ct);
            UseKestrel(0);

            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<GlzDbContext>();

            await db.Database.MigrateAsync(ct);
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task CleanupAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var select = new NpgsqlCommand(
            "SELECT external_subject FROM iam.iam_users",
            connection
        );

        var userIds = new List<string>();

        await using (var reader = await select.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                userIds.Add(reader.GetString(0));
            }
        }

        using var client = new HttpClient
        {
            BaseAddress = new Uri(_keycloak.GetBaseAddress())
        };

        using var tokenResponse = await client.PostAsync(
            "/realms/master/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = "admin-cli",
                ["username"] = "admin",
                ["password"] = "integration-admin"
            })
        );

        tokenResponse.EnsureSuccessStatusCode();

        var token = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token.GetProperty("access_token").GetString()
        );

        foreach (var userId in userIds)
        {
            using var response = await client.DeleteAsync(
                $"/admin/realms/glaziovi/users/{Uri.EscapeDataString(userId)}"
            );

            response.EnsureSuccessStatusCode();
        }

        await using var command = new NpgsqlCommand(
            "TRUNCATE TABLE persons.person_profiles, iam.iam_users RESTART IDENTITY CASCADE",
            connection
        );

        await command.ExecuteNonQueryAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultDatabase"] = _connectionString,
                ["Keycloak:Default:BaseUrl"] = _keycloak.GetBaseAddress().TrimEnd('/'),
                ["Keycloak:Default:Realm"] = "glaziovi",
                ["Keycloak:Default:ClientId"] = "glaziovi-api",
                ["Keycloak:Default:ClientSecret"] = "integration-client"
            });
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _keycloak.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private async Task ExecuteAdminSqlAsync(string sql, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(
            _postgres.GetConnectionString()
        );

        await connection.OpenAsync(ct);

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }
}
