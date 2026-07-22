using Microsoft.AspNetCore.Components;
using USPSimGame.Data.Entities;
using USPSimGame.Services;

namespace USPSimGame.Components.Creator;

public partial class TeamsOverview : ComponentBase
{
    [Inject]
    public ITeamService TeamService { get; set; } = default!;

    [Inject]
    public IGameSessionService GameSessionService { get; set; } = default!;

    protected List<GameSession>? GameSessions { get; set; }
    protected List<Team>? Teams { get; set; }
    protected int SelectedSessionId { get; set; }
    protected bool IsLoading { get; set; } = true;
    protected bool ShowForm { get; set; }
    protected Team? EditingTeam { get; set; }
    protected string? ErrorMessage { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await LoadSessionsAndTeamsAsync();
    }

    protected async Task LoadSessionsAndTeamsAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            GameSessions = await GameSessionService.GetGameSessionsAsync();
            if (GameSessions != null && GameSessions.Any())
            {
                if (SelectedSessionId == 0 || !GameSessions.Any(s => s.Id == SelectedSessionId))
                {
                    SelectedSessionId = GameSessions.First().Id;
                }
                await LoadTeamsForSelectedSessionAsync();
            }
            else
            {
                Teams = new List<Team>();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading data: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected async Task LoadTeamsForSelectedSessionAsync()
    {
        if (SelectedSessionId <= 0)
        {
            return;
        }

        IsLoading = true;
        try
        {
            Teams = await TeamService.GetTeamsByGameSessionAsync(SelectedSessionId);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading teams: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected async Task OnSessionChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out int sessionId))
        {
            SelectedSessionId = sessionId;
            ShowForm = false;
            EditingTeam = null;
            await LoadTeamsForSelectedSessionAsync();
        }
    }

    protected void StartAddTeam()
    {
        EditingTeam = null;
        ShowForm = true;
    }

    protected void StartEditTeam(Team team)
    {
        EditingTeam = team;
        ShowForm = true;
    }

    protected void CancelForm()
    {
        ShowForm = false;
        EditingTeam = null;
    }

    protected async Task HandleTeamSaved(Team team)
    {
        ShowForm = false;
        EditingTeam = null;
        await LoadTeamsForSelectedSessionAsync();
    }

    protected async Task DeleteTeam(int teamId)
    {
        try
        {
            var success = await TeamService.DeleteTeamAsync(teamId);
            if (success)
            {
                await LoadTeamsForSelectedSessionAsync();
            }
            else
            {
                ErrorMessage = "Failed to delete team. It may have already been removed.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error deleting team: {ex.Message}";
        }
    }
}
