using Microsoft.AspNetCore.Components;
using USPSimGame.Data.Entities;
using USPSimGame.Services;
using USPSimGame.Services.Costing;

namespace USPSimGame.Components.Game;

public partial class TeamBudgetWidget : ComponentBase, IDisposable
{
    [Inject] public ITeamService TeamService { get; set; } = default!;
    [Inject] public ITeamBudgetService TeamBudgetService { get; set; } = default!;
    [Inject] public IGameSessionNotifierService SessionNotifier { get; set; } = default!;
    [Inject] public IPlanNotifierService PlanNotifier { get; set; } = default!;

    [Parameter, EditorRequired] public int TeamId { get; set; }

    protected double CurrentBalance { get; set; } = 100;
    protected double MonthlyExpenseBurden { get; set; } = 0;

    protected override void OnInitialized()
    {
        SessionNotifier.OnGameSessionStateChanged += HandleSessionStateChangedAsync;
        PlanNotifier.OnPlansChanged += HandlePlansChangedAsync;
    }

    protected override async Task OnParametersSetAsync()
    {
        await LoadBudgetStatusAsync();
    }

    private async Task HandleSessionStateChangedAsync(GameSession session)
    {
        await InvokeAsync(RefreshAsync);
    }

    private async Task HandlePlansChangedAsync(int gameSessionId)
    {
        await InvokeAsync(RefreshAsync);
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

    public void Dispose()
    {
        SessionNotifier.OnGameSessionStateChanged -= HandleSessionStateChangedAsync;
        PlanNotifier.OnPlansChanged -= HandlePlansChangedAsync;
    }
}
