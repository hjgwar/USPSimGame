using Microsoft.EntityFrameworkCore;
using USPSimGame.Data.Entities;

namespace USPSimGame.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<GameSession> GameSessions => Set<GameSession>();
    public DbSet<PlayerSession> PlayerSessions => Set<PlayerSession>();
    public DbSet<MapLayerDefinition> MapLayerDefinitions => Set<MapLayerDefinition>();
    public DbSet<GameSessionMapLayer> GameSessionMapLayers => Set<GameSessionMapLayer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>().HasData(
            new User { Id = 1, Username = "Admin", Email = "harald.warmelink@hu.nl", PasswordHash = "" }
        );

        modelBuilder.Entity<Team>()
            .HasOne<GameSession>()
            .WithMany(s => s.Teams)
            .HasForeignKey(t => t.GameSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GameSession>()
            .Property(s => s.State)
            .HasConversion<string>();

        modelBuilder.Entity<MapLayerDefinition>()
            .Property(l => l.LayerType)
            .HasConversion<string>();

        modelBuilder.Entity<MapLayerDefinition>()
            .Property(l => l.Category)
            .HasConversion<string>();

        modelBuilder.Entity<GameSessionMapLayer>()
            .HasOne(g => g.GameSession)
            .WithMany(s => s.MapLayers)
            .HasForeignKey(g => g.GameSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GameSessionMapLayer>()
            .HasOne(g => g.LayerDefinition)
            .WithMany()
            .HasForeignKey(g => g.MapLayerDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Seed initial MapLayerDefinition catalog items
        modelBuilder.Entity<MapLayerDefinition>().HasData(
            new MapLayerDefinition
            {
                Id = 1,
                Key = "pdok-3dbag-buildings",
                Name = "3D BAG Buildings (2.5D Extruded)",
                Description = "Extruded 3D building footprints derived from 3D BAG lidar elevation data (LoD 1.3).",
                LayerType = MapLayerType.VectorGeoJson,
                Category = MapLayerCategory.Buildings,
                IsEnabledByDefault = true,
                TranslatorTags = null,
                SimulatorTags = null
            },
            new MapLayerDefinition
            {
                Id = 2,
                Key = "liander-open-data-elektra",
                Name = "Liander Electricity Network (Cables & Stations)",
                Description = "Electrical grid infrastructure featuring low-, medium-, and high-voltage cables and transformer stations for Liander territory.",
                LayerType = MapLayerType.VectorGeoJson,
                Category = MapLayerCategory.Infrastructure,
                IsEnabledByDefault = false,
                TranslatorTags = null,
                SimulatorTags = null
            },
            new MapLayerDefinition
            {
                Id = 3,
                Key = "stedin-open-data-elektra",
                Name = "Stedin Regional Electricity Grid",
                Description = "Complete low-, medium-, and high-voltage electricity cables and transformer stations for Stedin service territory.",
                LayerType = MapLayerType.VectorGeoJson,
                Category = MapLayerCategory.Infrastructure,
                IsEnabledByDefault = false,
                TranslatorTags = null,
                SimulatorTags = null
            },
            new MapLayerDefinition
            {
                Id = 4,
                Key = "pdok-gwsw-sewage",
                Name = "Urban Sewage & Drainage Network (PDOK GWSW)",
                Description = "Municipal urban water, sewage pipes, inspection manholes, and pumping stations from Stichting Rioned GWSW.",
                LayerType = MapLayerType.VectorGeoJson,
                Category = MapLayerCategory.Infrastructure,
                IsEnabledByDefault = false,
                TranslatorTags = null,
                SimulatorTags = null
            },
            new MapLayerDefinition
            {
                Id = 5,
                Key = "pdok-brk-bestuurlijkegebieden",
                Name = "Municipal Boundaries (BRK Bestuurlijke Gebieden)",
                Description = "Official municipal boundaries and administrative jurisdiction borders derived from Kadaster BRK.",
                LayerType = MapLayerType.VectorGeoJson,
                Category = MapLayerCategory.Environment,
                IsEnabledByDefault = false,
                TranslatorTags = null,
                SimulatorTags = null
            },
            new MapLayerDefinition
            {
                Id = 6,
                Key = "pdok-brk-kadastralekaart",
                Name = "Cadastral Parcels (BRK Kadastrale Kaart WFS)",
                Description = "Official cadastral land parcel boundaries, plot sizes, section codes, and parcel numbers from Kadaster BRK.",
                LayerType = MapLayerType.VectorGeoJson,
                Category = MapLayerCategory.Environment,
                IsEnabledByDefault = false,
                TranslatorTags = null,
                SimulatorTags = null
            }
        );
    }
}
