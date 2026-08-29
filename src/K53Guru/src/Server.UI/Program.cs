using K53Guru.Application;
using K53Guru.Infrastructure;
using K53Guru.Infrastructure.Extensions;
using K53Guru.Server.UI;


var builder = WebApplication.CreateBuilder(args);
builder.RegisterSerilog();
builder.WebHost.UseStaticWebAssets();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddServerUI(builder.Configuration);
var app = builder.Build();

app.ConfigureServer(builder.Configuration);

await app.InitializeDatabaseAsync().ConfigureAwait(false);
app.InitializeCacheFactory();
await app.RunAsync().ConfigureAwait(false);
