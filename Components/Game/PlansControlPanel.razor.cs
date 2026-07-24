using Microsoft.AspNetCore.Components;
using USPSimGame.Data.Entities;
using USPSimGame.Services.Plans;

namespace USPSimGame.Components.Game;

public partial class PlansControlPanel : ComponentBase, IDisposable
{
    [Inject]
    public IPlanService PlanService { get; set; } = default!;

    [Parameter, EditorRequired]
    public int GameSessionId { get; set; }

    [Parameter]
    public int StartYear { get; set; } = 2026;

    [Parameter]
    public Plan? ActivePlan { get; set; }

    [Parameter]
    public EventCallback OnOpenNewPlan { get; set; }

    [Parameter]
    public EventCallback<Plan?> OnPlanSelected { get; set; }

    protected List<Plan> Plans { get; set; } = new();
    protected bool IsLoading { get; set; } = true;
    protected bool IsCollapsed { get; set; } = false;

    protected override async Task OnInitializedAsync()
    {
        PlanService.OnPlanCreated += HandlePlanCreatedAsync;
        await LoadPlansAsync();
    }

    private async Task HandlePlanCreatedAsync(int sessionId, Plan plan)
    {
        if (sessionId == GameSessionId)
        {
            await RefreshPlansAsync();
        }
    }

    public async Task RefreshPlansAsync()
    {
        await LoadPlansAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task LoadPlansAsync()
    {
        IsLoading = true;
        try
        {
            Plans = await PlanService.GetSessionPlansAsync(GameSessionId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlansControlPanel] Error loading plans: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected void ToggleCollapse()
    {
        IsCollapsed = !IsCollapsed;
    }

    protected async Task SelectPlanAsync(Plan plan)
    {
        if (ActivePlan?.Id == plan.Id)
        {
            await OnPlanSelected.InvokeAsync(null);
        }
        else
        {
            await OnPlanSelected.InvokeAsync(plan);
        }
    }

    protected async Task TriggerOpenNewPlanAsync()
    {
        await OnOpenNewPlan.InvokeAsync();
    }

    protected string FormatMonthYear(int startMonth)
    {
        int totalMonths = (StartYear * 12) + startMonth;
        int year = totalMonths / 12;
        int month = (totalMonths % 12) + 1;
        DateTime dt = new DateTime(year, month, 1);
        return $"{dt:MMM yyyy} (Month {startMonth})";
    }

    protected string GetStateBadgeClass(PlanState state)
    {
        return state switch
        {
            PlanState.Draft => "bg-secondary-subtle text-secondary border-secondary-subtle",
            PlanState.Requested => "bg-info-subtle text-info border-info-subtle",
            PlanState.Approved => "bg-primary-subtle text-primary border-primary-subtle",
            PlanState.Implemented => "bg-success-subtle text-success border-success-subtle",
            PlanState.Archived => "bg-dark-subtle text-muted border-dark-subtle",
            _ => "bg-light text-dark"
        };
    }

    public void Dispose()
    {
        PlanService.OnPlanCreated -= HandlePlanCreatedAsync;
    }
}
