using Glaziovi.Database;
using Microsoft.EntityFrameworkCore;

namespace Glaziovi.Web;

public static class DependencyInjection
{
    extension(WebApplicationBuilder builder)
    {
        public void InjectDependencies()
        {
            builder.InjectDatabase();
        }

        private void InjectDatabase()
        {
            builder.Services.AddDbContext<GlzDbContext>(opt =>
            {
                string connectionString = builder.Configuration.GetConnectionString("DefaultDatabase")!;

                opt.UseNpgsql(connectionString, x =>
                {
                    x.UseNetTopologySuite();
                });
            });
        }
    }
}
