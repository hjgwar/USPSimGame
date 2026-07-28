using Microsoft.AspNetCore.Components;
using USPSimGame.Data.Enums;
using USPSimGame.Services;

namespace USPSimGame.Components.Game;

public partial class GameOverlay : ComponentBase, IDisposable
{
    [Inject]
    public PlayerSessionState PlayerSessionState { get; set; } = default!;

    [Inject]
    public IGameSessionService GameSessionService { get; set; } = default!;

    [Inject]
    public NavigationManager Navigation { get; set; } = default!;

    protected bool ShowStateControlPanel { get; set; } = false;

    public string FormattedGameDate
    {
        get
        {
            var session = PlayerSessionState.CurrentGameSession;
            if (session == null)
            {
                return string.Empty;
            }

            int startYear = session.StartYear > 0 ? session.StartYear : 2026;
            return USPSimGame.Utils.CommonGameUtils.FormatMonthYear(session.CurrentMonth, startYear);
        }
    }

    protected override void OnInitialized()
    {
        GameSessionService.OnGameSessionStateChanged += HandleSessionStateChangedAsync;
    }

    private async Task HandleSessionStateChangedAsync(int sessionId, GameState newState)
    {
        if (PlayerSessionState.CurrentGameSession?.Id == sessionId)
        {
            PlayerSessionState.CurrentGameSession.State = newState;
            await InvokeAsync(StateHasChanged);
        }
    }

    protected void ToggleStateControlPanel()
    {
        if (PlayerSessionState.IsAdmin)
        {
            ShowStateControlPanel = !ShowStateControlPanel;
        }
    }

    protected async Task ChangeGameStateAsync(GameState newState)
    {
        var session = PlayerSessionState.CurrentGameSession;
        if (session != null && PlayerSessionState.IsAdmin)
        {
            session.State = newState;
            await GameSessionService.UpdateGameSessionStateAsync(session.Id, newState);
            ShowStateControlPanel = false;
        }
    }

    protected void Disconnect()
    {
        PlayerSessionState.ClearSession();
        Navigation.NavigateTo("/");
    }

    public void Dispose()
    {
        GameSessionService.OnGameSessionStateChanged -= HandleSessionStateChangedAsync;
    }
}
