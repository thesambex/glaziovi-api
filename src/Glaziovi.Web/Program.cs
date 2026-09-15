using Glaziovi.Web;
using Glaziovi.Web.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.InjectDependencies();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}


app.MapEndpoints();

app.Run();

public partial class Program;
