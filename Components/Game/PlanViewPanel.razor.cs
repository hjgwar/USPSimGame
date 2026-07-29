using Microsoft.AspNetCore.Components;
using USPSimGame.Data.Entities;
using USPSimGame.Services;
using USPSimGame.Services.Plans;

namespace USPSimGame.Components.Game;

public partial class PlanViewPanel : ComponentBase, IDisposable
{
    [Inject] public IPlanService PlanService { get; set; } = default!;
    [Inject] public IPlanNotifierService PlanNotifier { get; set; } = default!;
    [Inject] public IPlanApprovalEvaluationService EvaluationService { get; set; } = default!;
    [Inject] public USPSimGame.Services.CreatorAuthState AdminAuthState { get; set; } = default!;

    [Parameter, EditorRequired] public Plan Plan { get; set; } = default!;
    [Parameter] public int StartYear { get; set; } = 2026;
    [Parameter] public int CurrentTeamId { get; set; }
    [Parameter] public int CurrentPlayerSessionId { get; set; }
    [Parameter] public List<Team> SessionTeams { get; set; } = new();
    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public EventCallback<Plan> OnEditPlan { get; set; }

    protected bool IsDropdownOpen { get; set; } = false;
    protected bool IsJudgmentPanelOpen { get; set; } = false;

    protected PlanApprovalEvaluation? Evaluation { get; set; }
    protected List<PlanTeamJudgment> Judgments { get; set; } = new();

    protected bool CanEdit => Plan.State == PlanState.Draft &&
                             Plan.TeamId == CurrentTeamId &&
                             (string.IsNullOrEmpty(Plan.LockedBySessionId) || Plan.LockedBySessionId == CurrentPlayerSessionId.ToString());

    protected bool IsLockedByOther => !string.IsNullOrEmpty(Plan.LockedBySessionId) &&
                                     Plan.LockedBySessionId != CurrentPlayerSessionId.ToString();

    protected bool CanChangeState => (Plan.TeamId == CurrentTeamId || AdminAuthState.IsAuthenticated) && Plan.State != PlanState.Implemented && Plan.State != PlanState.Implementing;

    protected bool CanApprovePlan
    {
        get
        {
            if (Evaluation == null || !Evaluation.RequiredTeamIds.Any()) return true;
            return Evaluation.RequiredTeamIds.All(teamId =>
            {
                var j = Judgments.FirstOrDefault(x => x.TeamId == teamId);
                return j != null && (j.Judgment == PlanJudgmentType.Approve || j.Judgment == PlanJudgmentType.Join);
            });
        }
    }

    protected bool HasRejections
    {
        get
        {
            if (Evaluation == null || !Evaluation.RequiredTeamIds.Any()) return false;
            return Evaluation.RequiredTeamIds.Any(teamId =>
            {
                var j = Judgments.FirstOrDefault(x => x.TeamId == teamId);
                return j != null && j.Judgment == PlanJudgmentType.Reject;
            });
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        await LoadEvaluationAndJudgmentsAsync();
    }

    protected override void OnInitialized()
    {
        PlanNotifier.OnPlansChanged += HandleJudgmentsUpdatedEvent;
    }

    private async Task HandleJudgmentsUpdatedEvent(int gameSessionId)
    {
        if (Plan != null && Plan.GameSessionId == gameSessionId)
        {
            var updatedPlan = await PlanService.GetPlanDetailsAsync(Plan.Id);
            if (updatedPlan != null)
            {
                Plan = updatedPlan;
            }
            await LoadEvaluationAndJudgmentsAsync();
            await InvokeAsync(StateHasChanged);
        }
    }

    protected async Task LoadEvaluationAndJudgmentsAsync()
    {
        if (Plan == null) return;
        try
        {
            Evaluation = await EvaluationService.EvaluatePlanAsync(Plan.Id);
            Judgments = await PlanService.GetPlanJudgmentsAsync(Plan.Id);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlanViewPanel] Error evaluating plan: {ex.Message}");
        }
    }

    protected List<PlanState> GetAvailableStates()
    {
        var states = new List<PlanState>();
        if (Evaluation == null) return states;

        if (!Evaluation.HasGeometry)
        {
            states.Add(PlanState.Draft);
            states.Add(PlanState.Archived);
            return states;
        }

        states.Add(PlanState.Draft);
        states.Add(PlanState.Consultation);

        if (!Evaluation.RequiresMultiTeamApproval || AdminAuthState.IsAuthenticated)
        {
            states.Add(PlanState.Approved);
        }
        else
        {
            states.Add(PlanState.Requested);
        }

        states.Add(PlanState.Archived);
        return states;
    }

    protected void ToggleDropdown()
    {
        IsDropdownOpen = !IsDropdownOpen;
    }

    protected void OpenJudgmentPanel()
    {
        IsJudgmentPanelOpen = true;
    }

    protected async Task HandleJudgmentsUpdated()
    {
        await LoadEvaluationAndJudgmentsAsync();
        StateHasChanged();
    }

    protected async Task CloseAsync()
    {
        IsDropdownOpen = false;
        await OnClose.InvokeAsync();
    }

    protected async Task EditPlanAsync()
    {
        IsDropdownOpen = false;
        await OnEditPlan.InvokeAsync(Plan);
    }

    protected async Task ChangeStateAsync(PlanState newState)
    {
        IsDropdownOpen = false;
        if (newState == PlanState.Implemented || newState == PlanState.Implementing || !CanChangeState) return;

        Plan.State = newState;
        await PlanService.UpdatePlanStateAsync(Plan.Id, newState);
        await LoadEvaluationAndJudgmentsAsync();
    }

    protected string FormatMonthYear(int startMonth)
    {
        return USPSimGame.Utils.CommonGameUtils.FormatMonthYear(startMonth, StartYear);
    }

    protected string GetStateBadgeClass(PlanState state)
    {
        return state switch
        {
            PlanState.Draft => "bg-secondary-subtle text-secondary border-secondary-subtle",
            PlanState.Consultation => "bg-warning-subtle text-dark border-warning-subtle",
            PlanState.Requested => "bg-info-subtle text-info border-info-subtle",
            PlanState.Approved => "bg-primary-subtle text-primary border-primary-subtle",
            PlanState.Implementing => "bg-warning text-dark border-warning-subtle",
            PlanState.Implemented => "bg-success-subtle text-success border-success-subtle",
            PlanState.Archived => "bg-dark-subtle text-muted border-dark-subtle",
            _ => "bg-light text-dark"
        };
    }

    protected string GetJudgmentBadgeClass(PlanJudgmentType judgment)
    {
        return judgment switch
        {
            PlanJudgmentType.Approve => "bg-success-subtle text-success border-success-subtle",
            PlanJudgmentType.Join => "bg-primary-subtle text-primary border-primary-subtle",
            PlanJudgmentType.Reject => "bg-danger-subtle text-danger border-danger-subtle",
            _ => "bg-secondary-subtle text-secondary border-secondary-subtle"
        };
    }

    public void Dispose()
    {
        PlanNotifier.OnPlansChanged -= HandleJudgmentsUpdatedEvent;
    }
}
