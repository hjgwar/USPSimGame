using Microsoft.EntityFrameworkCore;
using USPSimGame.Data;
using USPSimGame.Data.Entities;

namespace USPSimGame.Services;

public class TeamService : ITeamService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IPasswordHasher _passwordHasher;

    public TeamService(IDbContextFactory<AppDbContext> dbContextFactory, IPasswordHasher passwordHasher)
    {
        _dbContextFactory = dbContextFactory;
        _passwordHasher = passwordHasher;
    }

    public async Task<List<Team>> GetTeamsByGameSessionAsync(int gameSessionId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        return await db.Teams
            .Where(t => t.GameSessionId == gameSessionId)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<Team?> GetTeamByIdAsync(int id)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        return await db.Teams.FindAsync(id);
    }

    public async Task<Team> CreateTeamAsync(Team team, string plainPassword)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        team.PasswordHash = _passwordHasher.HashPassword(plainPassword);
        if (string.IsNullOrWhiteSpace(team.Color))
        {
            team.Color = "#3b82f6";
        }

        db.Teams.Add(team);
        await db.SaveChangesAsync();
        return team;
    }

    public async Task<Team> UpdateTeamAsync(Team team, string? newPlainPassword)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var existing = await db.Teams.FindAsync(team.Id);
        if (existing == null)
        {
            throw new KeyNotFoundException($"Team with ID {team.Id} was not found.");
        }

        existing.Name = team.Name;
        existing.Color = string.IsNullOrWhiteSpace(team.Color) ? "#3b82f6" : team.Color;

        if (!string.IsNullOrEmpty(newPlainPassword))
        {
            existing.PasswordHash = _passwordHasher.HashPassword(newPlainPassword);
        }

        await db.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteTeamAsync(int id)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var team = await db.Teams.FindAsync(id);
        if (team == null)
        {
            return false;
        }

        db.Teams.Remove(team);
        await db.SaveChangesAsync();
        return true;
    }
}
