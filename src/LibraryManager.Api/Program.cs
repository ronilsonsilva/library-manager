using LibraryManager.Api.Health;
using LibraryManager.Api.OpenApi;
using LibraryManager.Api.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLibraryManagerAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddLibraryManagerSwagger(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseLibraryManagerSwagger();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoints();
app.MapSecurityProbes();

app.Run();

public partial class Program;
