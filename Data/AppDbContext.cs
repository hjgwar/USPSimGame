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
    public DbSet<PlannableLayerDefinition> PlannableLayerDefinitions => Set<PlannableLayerDefinition>();
    public DbSet<GameSessionPlannableLayer> GameSessionPlannableLayers => Set<GameSessionPlannableLayer>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>().HasData(
            new User { Id = 1, Username = "Admin", Email = "harald.warmelink@hu.nl", PasswordHash = "AQAAAAIAAYagAAAAEKstQTVmO/0bmR5/P2B+mTIYP9Ju76yHdGFRYt7uq9Im2XkmV3pwZpvDAMTmlgzY3w==" }
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

        // PlannableLayer & Spatial Plan Conversions and Relationships
        modelBuilder.Entity<PlannableLayerDefinition>()
            .Property(p => p.Category)
            .HasConversion<string>();

        modelBuilder.Entity<PlannableLayerDefinition>()
            .Property(p => p.GeometryType)
            .HasConversion<string>();

        modelBuilder.Entity<GameSessionPlannableLayer>()
            .HasOne(g => g.GameSession)
            .WithMany(s => s.PlannableLayers)
            .HasForeignKey(g => g.GameSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GameSessionPlannableLayer>()
            .HasOne(g => g.PlannableLayerDefinition)
            .WithMany()
            .HasForeignKey(g => g.PlannableLayerDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Plan>()
            .Property(p => p.State)
            .HasConversion<string>();

        modelBuilder.Entity<Plan>()
            .HasOne(p => p.GameSession)
            .WithMany(s => s.Plans)
            .HasForeignKey(p => p.GameSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Plan>()
            .HasOne(p => p.Team)
            .WithMany()
            .HasForeignKey(p => p.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PlanFeature>()
            .HasOne(pf => pf.Plan)
            .WithMany(p => p.Features)
            .HasForeignKey(pf => pf.PlanId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PlanFeature>()
            .HasOne(pf => pf.GameSessionPlannableLayer)
            .WithMany()
            .HasForeignKey(pf => pf.GameSessionPlannableLayerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Seed initial MapLayerDefinition catalog items
        modelBuilder.Entity<MapLayerDefinition>().HasData(
            new MapLayerDefinition
            {
                Id = 1,
                Key = "pdok-3dbag-buildings",
                Name = "3D BAG Buildings",
                Description = "3D building footprints, roof shapes, heights, and volumes from 3D BAG (Kadaster/TU Delft).",
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
                Name = "Liander Electricity Grid",
                Description = "Low-, medium-, and high-voltage electricity grid network for Liander service territory.",
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

        // Seed initial PlannableLayerDefinition catalog items
        modelBuilder.Entity<PlannableLayerDefinition>().HasData(
            new PlannableLayerDefinition
            {
                Id = 1,
                Key = "solar-farm",
                Name = "Solar Farm Area",
                Description = "Zoned land polygon area designated for ground-mounted solar PV development.",
                Category = MapLayerCategory.Infrastructure,
                GeometryType = PlannableGeometryType.Polygon,
                Icon = "bi-sun-fill",
                DefaultColor = "#f59e0b",
                DefaultLineWidthPx = 2.5,
                IsEnabledByDefault = true
            },
            new PlannableLayerDefinition
            {
                Id = 2,
                Key = "wind-farm",
                Name = "Wind Farm Zone",
                Description = "Zoned land polygon area designated for onshore wind turbine installations.",
                Category = MapLayerCategory.Infrastructure,
                GeometryType = PlannableGeometryType.Polygon,
                Icon = "bi-wind",
                DefaultColor = "#06b6d4",
                DefaultLineWidthPx = 2.5,
                IsEnabledByDefault = true
            },
            new PlannableLayerDefinition
            {
                Id = 3,
                Key = "ev-charger-hub",
                Name = "EV Charging Station Hub",
                Description = "Public or commercial electric vehicle charging station hub point location.",
                Category = MapLayerCategory.Infrastructure,
                GeometryType = PlannableGeometryType.Point,
                Icon = "bi-ev-station-fill",
                DefaultColor = "#10b981",
                DefaultLineWidthPx = 2.0,
                IsEnabledByDefault = true
            },
            new PlannableLayerDefinition
            {
                Id = 4,
                Key = "power-cable",
                Name = "Electricity Connection Cable",
                Description = "High, medium, or low voltage power transmission or distribution line.",
                Category = MapLayerCategory.Infrastructure,
                GeometryType = PlannableGeometryType.Line,
                Icon = "bi-lightning-charge-fill",
                DefaultColor = "#3b82f6",
                DefaultLineWidthPx = 3.5,
                IsEnabledByDefault = true
            },
            new PlannableLayerDefinition
            {
                Id = 5,
                Key = "transformer-substation",
                Name = "Transformer Substation",
                Description = "Electrical grid transformer station or substations for voltage step-down/step-up.",
                Category = MapLayerCategory.Infrastructure,
                GeometryType = PlannableGeometryType.Point,
                Icon = "bi-box-seam",
                DefaultColor = "#ef4444",
                DefaultLineWidthPx = 2.0,
                IsEnabledByDefault = true
            }
        );
    }
}
