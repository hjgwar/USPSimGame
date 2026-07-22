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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>().HasData(
            new User { Id = 1, Username = "Admin", Email = "harald.warmelink@hu.nl", PasswordHash = "" }
        );

        modelBuilder.Entity<Team>()
            .HasOne<GameSession>()
            .WithMany()
            .HasForeignKey(t => t.GameSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GameSession>()
            .Property(s => s.State)
            .HasConversion<string>();
    }
}
