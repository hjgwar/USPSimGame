using Microsoft.AspNetCore.Components;
using USPSimGame.Data.Entities;
using USPSimGame.Services;

namespace USPSimGame.Components.Creator;

public partial class SessionTeamsModal : ComponentBase
{
    [Inject]
    public ITeamService TeamService { get; set; } = default!;

    [Parameter, EditorRequired]
    public GameSession Session { get; set; } = default!;

    [Parameter]
    public EventCallback OnClose { get; set; }

    [Parameter]
    public EventCallback OnUpdated { get; set; }

    protected List<Team> SessionTeams { get; set; } = new();
    protected string NewTeamName { get; set; } = string.Empty;
    protected string NewTeamPassword { get; set; } = string.Empty;
    protected string NewTeamColor { get; set; } = "#3b82f6";
    protected bool IsLoading { get; set; } = true;

    protected override async Task OnInitializedAsync()
    {
        await LoadSessionTeamsAsync();
    }

    protected async Task LoadSessionTeamsAsync()
    {
        IsLoading = true;
        try
        {
            SessionTeams = await TeamService.GetTeamsByGameSessionAsync(Session.Id);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SessionTeamsModal] Error loading teams: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected void ResetNewTeamForm()
    {
        NewTeamName = string.Empty;
        NewTeamPassword = string.Empty;
        NewTeamColor = "#3b82f6";
    }

    protected async Task AddTeamToSessionAsync()
    {
        if (!string.IsNullOrWhiteSpace(NewTeamName) && !string.IsNullOrWhiteSpace(NewTeamPassword))
        {
            try
            {
                var newTeam = new Team
                {
                    GameSessionId = Session.Id,
                    Name = NewTeamName,
                    Color = NewTeamColor
                };

                await TeamService.CreateTeamAsync(newTeam, NewTeamPassword);
                ResetNewTeamForm();
                await LoadSessionTeamsAsync();
                await OnUpdated.InvokeAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SessionTeamsModal] Error creating team: {ex.Message}");
            }
        }
    }

    protected async Task DeleteTeamFromSessionAsync(int teamId)
    {
        try
        {
            await TeamService.DeleteTeamAsync(teamId);
            await LoadSessionTeamsAsync();
            await OnUpdated.InvokeAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SessionTeamsModal] Error deleting team: {ex.Message}");
        }
    }

    protected async Task CloseAsync()
    {
        await OnClose.InvokeAsync();
    }
}
