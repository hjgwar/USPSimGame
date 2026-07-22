using Microsoft.AspNetCore.Components;
using USPSimGame.Data.Entities;

namespace USPSimGame.Components.Pages;

public partial class Home : ComponentBase
{
    protected GameSession? SelectedSession { get; set; }

    protected void HandleSessionSelected(GameSession session)
    {
        SelectedSession = session;
    }
}
