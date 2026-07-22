using Microsoft.AspNetCore.Components;
using USPSimGame.Data.Entities;
using USPSimGame.Services;

namespace USPSimGame.Components.Creator;

public partial class AddGameSessionForm : ComponentBase
{
    [Inject]
    public IGameSessionService GameSessionService { get; set; } = default!;

    [Parameter]
    public EventCallback<GameSession> OnSessionAdded { get; set; }

    [Parameter]
    public EventCallback OnCancel { get; set; }

    protected GameSession NewSession { get; set; } = new()
    {
        Name = "New Game Session",
        CenterLatLong = "5.17516, 52.08640",
        Zoom = 15,
        StartYear = 2026,
        CurrentMonth = 1,
        State = "Setup"
    };

    protected string? ErrorMessage { get; set; }
    protected bool IsSubmitting { get; set; }

    protected async Task HandleSubmit()
    {
        ErrorMessage = null;
        IsSubmitting = true;

        try
        {
            var created = await GameSessionService.CreateGameSessionAsync(NewSession);
            await OnSessionAdded.InvokeAsync(created);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to create game session: {ex.Message}";
        }
        finally
        {
            IsSubmitting = false;
        }
    }
}
