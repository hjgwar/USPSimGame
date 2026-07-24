using Microsoft.AspNetCore.Components;
using USPSimGame.Data.Entities;
using USPSimGame.Services.Plans;

namespace USPSimGame.Components.Game;

public partial class PlanViewPanel : ComponentBase
{
    [Inject]
    public IPlanService PlanService { get; set; } = default!;

    [Parameter, EditorRequired]
    public Plan Plan { get; set; } = default!;

    [Parameter]
    public int StartYear { get; set; } = 2026;

    [Parameter]
    public int CurrentTeamId { get; set; }

    [Parameter]
    public int CurrentPlayerSessionId { get; set; }

    [Parameter]
    public EventCallback OnClose { get; set; }

    [Parameter]
    public EventCallback<Plan> OnEditPlan { get; set; }

    protected bool IsDropdownOpen { get; set; } = false;

    protected bool CanEdit => Plan.State == PlanState.Draft &&
                             Plan.TeamId == CurrentTeamId &&
                             (string.IsNullOrEmpty(Plan.LockedBySessionId) || Plan.LockedBySessionId == CurrentPlayerSessionId.ToString());

    protected bool IsLockedByOther => !string.IsNullOrEmpty(Plan.LockedBySessionId) &&
                                     Plan.LockedBySessionId != CurrentPlayerSessionId.ToString();

    protected bool CanChangeState => Plan.TeamId == CurrentTeamId && Plan.State != PlanState.Implemented;

    protected void ToggleDropdown()
    {
        IsDropdownOpen = !IsDropdownOpen;
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
        if (newState == PlanState.Implemented || !CanChangeState) return;

        Plan.State = newState;
        await PlanService.UpdatePlanStateAsync(Plan.Id, newState);
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
            PlanState.Implemented => "bg-success-subtle text-success border-success-subtle",
            PlanState.Archived => "bg-dark-subtle text-muted border-dark-subtle",
            _ => "bg-light text-dark"
        };
    }
}
