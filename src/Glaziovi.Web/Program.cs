using Glaziovi.Web;

var builder = WebApplication.CreateBuilder(args);
builder.InjectDependencies();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.Run();
