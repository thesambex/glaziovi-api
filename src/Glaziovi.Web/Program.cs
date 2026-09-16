using Glaziovi.Keycloak;
using Glaziovi.Web;
using Glaziovi.Web.Endpoints;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.InjectDependencies();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(opt =>
    {
        var keycloak = builder.Configuration.GetSection("Keycloak:Default").Get<KeycloakOptions>()!;

        opt.AddAuthorizationCodeFlow("oauth2", flow =>
        {
            flow.ClientId = keycloak.ClientId;
            flow.SelectedScopes = ["openid", "profile"];
        });
    });
}

app.UseExceptionHandler();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapEndpoints();

app.Run();

public partial class Program;
