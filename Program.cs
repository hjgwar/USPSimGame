using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using USPSimGame.Components;
using USPSimGame.Data;
using USPSimGame.Services;
using USPSimGame.Services.Layers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(options =>
    {
        options.MaximumReceiveMessageSize = 50 * 1024 * 1024; // 50 MB
    });

builder.Services.AddBlazorBootstrap();

// Add Entity Framework Core & PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(connectionString)
           .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

// Register Application Services
builder.Services.AddSingleton<IPasswordHasher, PasswordHasherService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IGameSessionService, GameSessionService>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IPlayerSessionService, PlayerSessionService>();
builder.Services.AddScoped<CreatorAuthState>();
builder.Services.AddScoped<PlayerSessionState>();

// Register Building Service & Map Layer Strategy Providers
builder.Services.AddHttpClient<BuildingService>();
builder.Services.AddTransient<IBuildingService>(sp => sp.GetRequiredService<BuildingService>());
builder.Services.AddTransient<IMapLayerProvider>(sp => sp.GetRequiredService<BuildingService>());

builder.Services.AddHttpClient<LianderElektraService>();
builder.Services.AddTransient<IMapLayerProvider>(sp => sp.GetRequiredService<LianderElektraService>());

builder.Services.AddScoped<StedinElektraService>();
builder.Services.AddTransient<IMapLayerProvider>(sp => sp.GetRequiredService<StedinElektraService>());

builder.Services.AddHttpClient<PdokSewageWfsService>();
builder.Services.AddTransient<IMapLayerProvider>(sp => sp.GetRequiredService<PdokSewageWfsService>());

builder.Services.AddHttpClient<PdokBestuurlijkeGebiedenWfsService>();
builder.Services.AddTransient<IMapLayerProvider>(sp => sp.GetRequiredService<PdokBestuurlijkeGebiedenWfsService>());

builder.Services.AddHttpClient<PdokKadastraleKaartWfsService>();
builder.Services.AddTransient<IMapLayerProvider>(sp => sp.GetRequiredService<PdokKadastraleKaartWfsService>());

builder.Services.AddScoped<IMapLayerService, MapLayerService>();

var app = builder.Build();

// Automatically apply EF Core database migrations at application startup with retry loop
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    int maxRetries = 10;
    for (int retry = 1; retry <= maxRetries; retry++)
    {
        try
        {
            logger.LogInformation("Applying EF Core database migrations (attempt {Attempt}/{MaxRetries})...", retry, maxRetries);
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("EF Core database migrations applied successfully.");
            break;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database migration failed on attempt {Attempt}/{MaxRetries}.", retry, maxRetries);
            if (retry == maxRetries)
            {
                logger.LogCritical("Could not apply database migrations after {MaxRetries} attempts.", maxRetries);
                throw;
            }
            await Task.Delay(2000);
        }
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

// Minimal API Endpoint to stream pre-cached layer GeoJSON payloads directly to browser
app.MapGet("/api/layers/{sessionId:int}/{layerKey}", async (int sessionId, string layerKey, AppDbContext db) =>
{
    var layer = await db.GameSessionMapLayers
        .Include(l => l.LayerDefinition)
        .FirstOrDefaultAsync(l => l.GameSessionId == sessionId && l.LayerDefinition.Key == layerKey && l.IsEnabled);

    if (layer == null || string.IsNullOrEmpty(layer.CachedDataContent))
    {
        return Results.NotFound();
    }

    return Results.Content(layer.CachedDataContent, "application/json");
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
