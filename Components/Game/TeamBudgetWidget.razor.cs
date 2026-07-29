using Microsoft.AspNetCore.Components;
using USPSimGame.Services;
using USPSimGame.Services.Costing;
using USPSimGame.Services.Layers;

namespace USPSimGame.Components.Game;

public partial class TeamBudgetWidget : ComponentBase
{
    [Inject] public ITeamService TeamService { get; set; } = default!;
    [Inject] public ITeamBudgetService TeamBudgetService { get; set; } = default!;

    [Parameter, EditorRequired] public int TeamId { get; set; }

    protected double CurrentBalance { get; set; } = 100;
    protected double MonthlyExpenseBurden { get; set; } = 0;

    protected override async Task OnParametersSetAsync()
    {
        await LoadBudgetStatusAsync();
    }

    public async Task RefreshAsync()
    {
        await LoadBudgetStatusAsync();
        StateHasChanged();
    }

    private async Task LoadBudgetStatusAsync()
    {
        if (TeamId <= 0) return;
        try
        {
            var team = await TeamService.GetTeamByIdAsync(TeamId);
            if (team != null)
            {
                CurrentBalance = team.InvestmentPointsBalance;
            }
            MonthlyExpenseBurden = await TeamBudgetService.GetTeamMonthlyExpenseBurdenAsync(TeamId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TeamBudgetWidget] Error loading budget status: {ex.Message}");
        }
    }
}
