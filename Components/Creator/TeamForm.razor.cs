using Microsoft.AspNetCore.Components;
using USPSimGame.Data.Entities;
using USPSimGame.Services;

namespace USPSimGame.Components.Creator;

public partial class TeamForm : ComponentBase
{
    [Inject]
    public ITeamService TeamService { get; set; } = default!;

    [Parameter]
    public Team? TeamToEdit { get; set; }

    [Parameter, EditorRequired]
    public int GameSessionId { get; set; }

    [Parameter]
    public EventCallback<Team> OnSaved { get; set; }

    [Parameter]
    public EventCallback OnCancel { get; set; }

    protected Team TeamModel { get; set; } = new();
    protected string PlainPassword { get; set; } = string.Empty;
    protected string EditingTeamName { get; set; } = string.Empty;
    protected bool IsEditMode => TeamToEdit != null;
    protected string? ErrorMessage { get; set; }
    protected bool IsSubmitting { get; set; }

    protected override void OnParametersSet()
    {
        if (TeamToEdit != null)
        {
            TeamModel = new Team
            {
                Id = TeamToEdit.Id,
                GameSessionId = TeamToEdit.GameSessionId,
                Name = TeamToEdit.Name,
                Color = string.IsNullOrWhiteSpace(TeamToEdit.Color) ? "#3b82f6" : TeamToEdit.Color,
                PasswordHash = TeamToEdit.PasswordHash
            };
            EditingTeamName = TeamToEdit.Name;
        }
        else
        {
            TeamModel = new Team
            {
                GameSessionId = GameSessionId,
                Name = string.Empty,
                Color = "#3b82f6"
            };
            EditingTeamName = string.Empty;
        }

        PlainPassword = string.Empty;
    }

    protected async Task HandleSubmit()
    {
        ErrorMessage = null;
        IsSubmitting = true;

        try
        {
            Team savedTeam;
            if (IsEditMode)
            {
                savedTeam = await TeamService.UpdateTeamAsync(TeamModel, PlainPassword);
            }
            else
            {
                TeamModel.GameSessionId = GameSessionId;
                savedTeam = await TeamService.CreateTeamAsync(TeamModel, PlainPassword);
            }

            await OnSaved.InvokeAsync(savedTeam);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error saving team: {ex.Message}";
        }
        finally
        {
            IsSubmitting = false;
        }
    }
}
