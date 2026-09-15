using Glaziovi.Web.Endpoints.Profile;

namespace Glaziovi.Web.Endpoints;

public static class EndpointsMapping
{
    extension(WebApplication app)
    {
        public void MapEndpoints()
        {
            app.MapProfileEndpoints();
        }
    }
}
