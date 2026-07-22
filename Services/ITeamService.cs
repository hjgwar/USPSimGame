using USPSimGame.Data.Entities;

namespace USPSimGame.Services;

public interface ITeamService
{
    Task<List<Team>> GetTeamsByGameSessionAsync(int gameSessionId);
    Task<Team?> GetTeamByIdAsync(int id);
    Task<Team> CreateTeamAsync(Team team, string plainPassword);
    Task<Team> UpdateTeamAsync(Team team, string? newPlainPassword);
    Task<bool> DeleteTeamAsync(int id);
}
