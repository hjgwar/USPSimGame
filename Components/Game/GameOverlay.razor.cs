using Microsoft.AspNetCore.Components;
using USPSimGame.Services;

namespace USPSimGame.Components.Game;

public partial class GameOverlay : ComponentBase
{
    [Inject]
    public PlayerSessionState PlayerSessionState { get; set; } = default!;

    [Inject]
    public NavigationManager Navigation { get; set; } = default!;

    public string FormattedGameDate
    {
        get
        {
            var session = PlayerSessionState.CurrentGameSession;
            if (session == null)
            {
                return string.Empty;
            }

            int startYear = session.StartYear > 0 ? session.StartYear : DateTime.Now.Year;
            var baseDate = new DateTime(startYear, 1, 1);
            var currentDate = baseDate.AddMonths(session.CurrentMonth);
            return currentDate.ToString("MMM yyyy");
        }
    }

    protected void Disconnect()
    {
        PlayerSessionState.ClearSession();
        Navigation.NavigateTo("/");
    }
}
