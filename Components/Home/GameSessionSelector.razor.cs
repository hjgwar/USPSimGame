using Microsoft.AspNetCore.Components;
using USPSimGame.Data.Entities;
using USPSimGame.Data.Enums;
using USPSimGame.Services;

namespace USPSimGame.Components.Home;

public partial class GameSessionSelector : ComponentBase
{
    [Inject]
    public IGameSessionService GameSessionService { get; set; } = default!;

    [Parameter]
    public EventCallback<GameSession> OnSessionSelected { get; set; }

    [Parameter]
    public int SelectedSessionId { get; set; }

    protected List<GameSession>? GameSessions { get; set; }
    protected bool IsLoading { get; set; } = true;

    protected override async Task OnInitializedAsync()
    {
        IsLoading = true;
        try
        {
            GameSessions = await GameSessionService.GetGameSessionsAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected async Task SelectSession(GameSession session)
    {
        SelectedSessionId = session.Id;
        await OnSessionSelected.InvokeAsync(session);
    }
}
