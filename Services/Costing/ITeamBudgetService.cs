namespace USPSimGame.Services.Costing;

public interface ITeamBudgetService
{
    Task ProcessMonthlySimulationTickAsync(int gameSessionId, int currentYear, int currentMonth);
    Task ExecutePlanImplementationCostsAsync(int planId);
    Task<double> GetTeamMonthlyExpenseBurdenAsync(int teamId);
}
