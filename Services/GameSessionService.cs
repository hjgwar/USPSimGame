using Microsoft.EntityFrameworkCore;
using USPSimGame.Data;
using USPSimGame.Data.Entities;

namespace USPSimGame.Services;

public class GameSessionService : IGameSessionService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public GameSessionService(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<List<GameSession>> GetGameSessionsAsync()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        return await db.GameSessions.OrderByDescending(s => s.CreatedAt).ToListAsync();
    }

    public async Task<GameSession> CreateGameSessionAsync(GameSession session)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        session.CreatedAt = DateTime.UtcNow;
        db.GameSessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    public async Task<bool> DeleteGameSessionAsync(int sessionId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var session = await db.GameSessions.FindAsync(sessionId);
        if (session == null)
        {
            return false;
        }

        var relatedTeams = await db.Teams.Where(t => t.GameSessionId == sessionId).ToListAsync();
        if (relatedTeams.Any())
        {
            db.Teams.RemoveRange(relatedTeams);
        }

        db.GameSessions.Remove(session);
        await db.SaveChangesAsync();
        return true;
    }
}
