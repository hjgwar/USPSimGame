using USPSimGame.Data.Entities;

namespace USPSimGame.Services;

public class PlayerSessionState
{
    public PlayerSession? CurrentPlayerSession { get; private set; }
    public Team? CurrentTeam { get; private set; }
    public GameSession? CurrentGameSession { get; private set; }

    public bool IsConnected => CurrentPlayerSession != null && CurrentPlayerSession.IsActive;

    public event Action? OnStateChanged;

    public void SetSession(PlayerSession playerSession, Team team, GameSession gameSession)
    {
        CurrentPlayerSession = playerSession;
        CurrentTeam = team;
        CurrentGameSession = gameSession;
        NotifyStateChanged();
    }

    public void ClearSession()
    {
        CurrentPlayerSession = null;
        CurrentTeam = null;
        CurrentGameSession = null;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnStateChanged?.Invoke();
}
