using USPSimGame.Data.Entities;

namespace USPSimGame.Services;

public interface IGameSessionService
{
    Task<List<GameSession>> GetGameSessionsAsync();
    Task<GameSession> CreateGameSessionAsync(GameSession session);
    Task<bool> DeleteGameSessionAsync(int sessionId);
}
