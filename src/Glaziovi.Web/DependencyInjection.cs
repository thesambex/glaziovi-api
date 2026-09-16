using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Glaziovi.Core.Database;
using Glaziovi.Core.Providers;
using Glaziovi.Core.Services;
using Glaziovi.Database;
using Glaziovi.Database.Repositories.Iam;
using Glaziovi.Database.Repositories.Persons;
using Glaziovi.Infrastructure.Services;
using Glaziovi.Keycloak;
using Glaziovi.Modules.Iam.Repositories;
using Glaziovi.Modules.Persons.Repositories;
using Glaziovi.Web.Middlewares;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace Glaziovi.Web;

public static class DependencyInjection
{
    extension(WebApplicationBuilder builder)
    {
        public void InjectDependencies()
        {
            builder.Services.Configure<KeycloakOptions>(builder.Configuration.GetSection("Keycloak:Default"));

            builder.Services.AddExceptionHandler<GlobalExceptionHandlerMiddleware>();

            builder.Services.AddProblemDetails();
            builder.Services.AddValidation();

            builder.Services.ConfigureHttpJsonOptions(opt =>
            {
                opt.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

            builder.InjectOpenApi();
            builder.InjectDatabase();
            builder.InjectServices();
            builder.InjectSecurity();
        }

        private void InjectOpenApi()
        {
            builder.Services.AddOpenApi(opt =>
            {
                opt.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;

                opt.AddDocumentTransformer((document, _, _) =>
                {
                    var keycloak = builder.Configuration.GetSection("Keycloak:Default").Get<KeycloakOptions>()!;

                    document.Info.Version = "v1";
                    document.Info.Title = "Glaziovi API";
                    document.Info.Description = "API to integrate with Glaziovi ecosystem.";

                    string authUrl = $"{keycloak.BaseUrl}/realms/{keycloak.Realm}/protocol/openid-connect/auth";
                    string tokenUrl = $"{keycloak.BaseUrl}/realms/{keycloak.Realm}/protocol/openid-connect/token";

                    document.Components ??= new OpenApiComponents();
                    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

                    document.Components.SecuritySchemes["oauth2"] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.OAuth2,
                        Flows = new OpenApiOAuthFlows
                        {
                            AuthorizationCode = new OpenApiOAuthFlow
                            {
                                AuthorizationUrl = new Uri(authUrl),
                                TokenUrl = new Uri(tokenUrl),
                                Scopes = new Dictionary<string, string>
                                {
                                    { "openid", "OpenId" }, { "profile", "Profile" }
                                }
                            }
                        }
                    };

                    return Task.CompletedTask;
                });

                opt.AddOperationTransformer((operation, context, _) =>
                {
                    bool hasAllowAnonymous = context.Description.ActionDescriptor.EndpointMetadata
                        .OfType<AllowAnonymousAttribute>()
                        .Any();

                    if (hasAllowAnonymous)
                    {
                        return Task.CompletedTask;
                    }

                    operation.Security ??= new List<OpenApiSecurityRequirement>();

                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("oauth2")] = []
                    });

                    return Task.CompletedTask;
                });

                opt.AddSchemaTransformer((schema, context, _) =>
                {
                    if (context.JsonPropertyInfo?.PropertyType.IsEnum != true || schema.Enum?.Any() == true)
                        return Task.CompletedTask;

                    var values = Enum.GetValues(context.JsonPropertyInfo.PropertyType).Cast<Enum>();

                    schema.Enum = [.. values.Select(v => v.ToString()),];

                    return Task.CompletedTask;
                });
            });
        }

        private void InjectDatabase()
        {
            builder.Services.AddDbContext<GlzDbContext>(opt =>
            {
                string connectionString = builder.Configuration.GetConnectionString("DefaultDatabase")!;

                opt.UseNpgsql(connectionString, x => { x.UseNetTopologySuite(); });
            });

            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            builder.Services.AddTransient<IUserRepository, UserRepository>();
            builder.Services.AddTransient<IPersonProfileRepository, PersonProfileRepository>();
        }

        private void InjectServices()
        {
            builder.Services.AddScoped<IProfileService, ProfileService>();

            builder.Services.AddHttpClient<IIdentityProvider, KeycloakProvider>(client =>
            {
                client.BaseAddress = new Uri(builder.Configuration.GetSection("Keycloak:Default:BaseUrl").Value!);
            });
        }

        private void InjectSecurity()
        {
            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(opt =>
                {
                    var keycloak = builder.Configuration.GetSection("Keycloak:Default").Get<KeycloakOptions>()!;

                    string authorityUrl = $"{keycloak.BaseUrl}/realms/{keycloak.Realm}";

                    opt.Authority = authorityUrl;
                    opt.Audience = keycloak.ClientId;
                    opt.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

                    opt.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidIssuer = authorityUrl,
                        ValidateIssuer = true,
                        ValidAudiences =
                        [
                            keycloak.ClientId,
                            "account"
                        ],
                        ValidateAudience = true,
                        NameClaimType = "preferred_username",
                        RoleClaimType = ClaimTypes.Role
                    };

                    opt.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = context =>
                        {
                            var identity = context.Principal!.Identity as ClaimsIdentity;

                            string? realmAccess = context.Principal.FindFirst("realm_access")?.Value;
                            if (realmAccess is not null)
                            {
                                var roles = JsonSerializer
                                    .Deserialize<JsonElement>(realmAccess)
                                    .GetProperty("roles")
                                    .EnumerateArray()
                                    .Select(r => r.GetString());

                                foreach (var role in roles!)
                                {
                                    if (!identity!.HasClaim(ClaimTypes.Role, role!))
                                    {
                                        identity.AddClaim(new Claim(ClaimTypes.Role, role!));
                                    }
                                }
                            }

                            string? resourceAccess = context.Principal.FindFirst("resource_access")?.Value;
                            if (resourceAccess is null)
                            {
                                return Task.CompletedTask;
                            }

                            var json = JsonSerializer.Deserialize<JsonElement>(resourceAccess);

                            if (!json.TryGetProperty(keycloak.ClientId, out var client) ||
                                !client.TryGetProperty("roles", out var clientRoles))
                            {
                                return Task.CompletedTask;
                            }

                            foreach (string? roleName in clientRoles.EnumerateArray().Select(role => role.GetString())
                                         .Where(roleName => !identity!.HasClaim(ClaimTypes.Role, roleName!)))
                            {
                                identity!.AddClaim(new Claim(ClaimTypes.Role, roleName!));
                            }

                            return Task.CompletedTask;
                        }
                    };
                });

            builder.Services.AddAuthorization();
        }
    }
}
