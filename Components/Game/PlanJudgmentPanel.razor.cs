using Microsoft.AspNetCore.Components;
using USPSimGame.Data.Entities;
using USPSimGame.Services.Plans;
using USPSimGame.Services;

namespace USPSimGame.Components.Game;

public partial class PlanJudgmentPanel : ComponentBase, IDisposable
{
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }

    [Parameter] public Plan? TargetPlan { get; set; }
    [Parameter] public List<Team> SessionTeams { get; set; } = new();
    [Parameter] public EventCallback OnJudgmentsUpdated { get; set; }

    private bool IsLoading { get; set; } = true;
    private PlanApprovalEvaluation? Evaluation { get; set; }
    private List<Team> RequiredTeams { get; set; } = new();
    private List<PlanTeamJudgment> Judgments { get; set; } = new();

    private int CurrentPlayerTeamId => PlayerState.CurrentTeam?.Id ?? 0;

    protected override async Task OnParametersSetAsync()
    {
        if (IsOpen && TargetPlan != null)
        {
            await LoadDataAsync();
        }
    }

    protected override void OnInitialized()
    {
        PlanService.OnPlanJudgmentsUpdated += HandleJudgmentsUpdated;
    }

    private async Task HandleJudgmentsUpdated(int gameSessionId)
    {
        if (IsOpen && TargetPlan != null && TargetPlan.GameSessionId == gameSessionId)
        {
            await LoadDataAsync();
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task LoadDataAsync()
    {
        if (TargetPlan == null) return;
        IsLoading = true;

        try
        {
            Evaluation = await EvaluationService.EvaluatePlanAsync(TargetPlan.Id);
            Judgments = await PlanService.GetPlanJudgmentsAsync(TargetPlan.Id);

            RequiredTeams = SessionTeams
                .Where(t => Evaluation.RequiredTeamIds.Contains(t.Id))
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlanJudgmentPanel] Error loading judgments: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SubmitJudgment(int teamId, PlanJudgmentType judgment)
    {
        if (TargetPlan == null) return;
        try
        {
            await PlanService.SubmitTeamJudgmentAsync(TargetPlan.Id, teamId, judgment);
            await LoadDataAsync();
            if (OnJudgmentsUpdated.HasDelegate)
            {
                await OnJudgmentsUpdated.InvokeAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlanJudgmentPanel] Error submitting judgment: {ex.Message}");
        }
    }

    private async Task Close()
    {
        IsOpen = false;
        await IsOpenChanged.InvokeAsync(false);
    }

    public void Dispose()
    {
        PlanService.OnPlanJudgmentsUpdated -= HandleJudgmentsUpdated;
    }
}
