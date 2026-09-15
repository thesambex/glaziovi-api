using Glaziovi.Core.Providers;
using Glaziovi.Database;
using Glaziovi.Keycloak;
using Microsoft.EntityFrameworkCore;

namespace Glaziovi.Web;

public static class DependencyInjection
{
    extension(WebApplicationBuilder builder)
    {
        public void InjectDependencies()
        {
            builder.Services.Configure<KeycloakOptions>(builder.Configuration.GetSection("Keycloak:Default"));

            builder.InjectDatabase();
            builder.InjectServices();
        }

        private void InjectDatabase()
        {
            builder.Services.AddDbContext<GlzDbContext>(opt =>
            {
                string connectionString = builder.Configuration.GetConnectionString("DefaultDatabase")!;

                opt.UseNpgsql(connectionString, x => { x.UseNetTopologySuite(); });
            });
        }

        private void InjectServices()
        {
            builder.Services.AddHttpClient<IIdentityProvider, KeycloakProvider>(client =>
            {
                client.BaseAddress = new Uri(builder.Configuration.GetSection("Keycloak:Default:BaseUrl").Value!);
            });
        }
    }
}
