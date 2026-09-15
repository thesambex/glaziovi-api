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
    }
}
