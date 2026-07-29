using Microsoft.EntityFrameworkCore;
using USPSimGame.Data;
using USPSimGame.Data.Entities;

namespace USPSimGame.Services.Costing;

public class TeamBudgetService : ITeamBudgetService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ICostCalculationService _costCalculationService;

    public TeamBudgetService(IDbContextFactory<AppDbContext> dbContextFactory, ICostCalculationService costCalculationService)
    {
        _dbContextFactory = dbContextFactory;
        _costCalculationService = costCalculationService;
    }

    public async Task ProcessMonthlySimulationTickAsync(int gameSessionId, int currentYear, int currentMonth)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var teams = await context.Teams.Where(t => t.GameSessionId == gameSessionId).ToListAsync();
        if (!teams.Any()) return;

        // 1. January 1st Annual Budget Refill / Cap
        // Positive balances reset/capped to 100 max, while negative balances (debt) receive +100 pts allowance
        if (currentMonth == 1)
        {
            foreach (var team in teams)
            {
                if (team.InvestmentPointsBalance >= 0)
                {
                    team.InvestmentPointsBalance = Math.Min(team.InvestmentPointsBalance, team.AnnualBudgetAllowance);
                }
                else
                {
                    team.InvestmentPointsBalance += team.AnnualBudgetAllowance;
                }
            }
        }

        // 2. Monthly Expense Deductions for active Implemented Plans
        var activeImplementedPlans = await context.Plans
            .Include(p => p.Judgments)
            .Where(p => p.GameSessionId == gameSessionId && p.State == PlanState.Implemented)
            .ToListAsync();

        foreach (var plan in activeImplementedPlans)
        {
            var cost = await _costCalculationService.CalculatePlanCostAsync(plan.Id);
            if (cost.TotalMonthlyExpensePoints > 0)
            {
                var participatingTeamIds = new HashSet<int> { plan.TeamId };
                foreach (var j in plan.Judgments.Where(j => j.Judgment == PlanJudgmentType.Join))
                {
                    participatingTeamIds.Add(j.TeamId);
                }

                double monthlyShare = Math.Round(cost.TotalMonthlyExpensePoints / participatingTeamIds.Count, 1);
                foreach (var teamId in participatingTeamIds)
                {
                    var team = teams.FirstOrDefault(t => t.Id == teamId);
                    if (team != null)
                    {
                        team.InvestmentPointsBalance -= monthlyShare;
                    }
                }
            }
        }

        await context.SaveChangesAsync();
    }

    public async Task ExecutePlanImplementationCostsAsync(int planId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var plan = await context.Plans
            .Include(p => p.Judgments)
            .FirstOrDefaultAsync(p => p.Id == planId);

        if (plan == null) return;

        var cost = await _costCalculationService.CalculatePlanCostAsync(planId);
        plan.TotalCalculatedInvestmentPoints = cost.TotalInvestmentPoints;
        plan.TotalCalculatedMonthlyExpensePoints = cost.TotalMonthlyExpensePoints;
        plan.ExpenseDurationMonths = cost.ExpenseDurationMonths;

        var participatingTeamIds = new HashSet<int> { plan.TeamId };
        foreach (var j in plan.Judgments.Where(j => j.Judgment == PlanJudgmentType.Join))
        {
            participatingTeamIds.Add(j.TeamId);
        }

        double investmentShare = Math.Round(cost.TotalInvestmentPoints / participatingTeamIds.Count, 1);

        var teams = await context.Teams.Where(t => participatingTeamIds.Contains(t.Id)).ToListAsync();
        foreach (var team in teams)
        {
            team.InvestmentPointsBalance -= investmentShare;
        }

        await context.SaveChangesAsync();
    }

    public async Task<double> GetTeamMonthlyExpenseBurdenAsync(int teamId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var team = await context.Teams.FirstOrDefaultAsync(t => t.Id == teamId);
        if (team == null) return 0;

        var activeImplementedPlans = await context.Plans
            .Include(p => p.Judgments)
            .Where(p => p.GameSessionId == team.GameSessionId && p.State == PlanState.Implemented)
            .ToListAsync();

        double totalMonthlyExpenseBurden = 0;

        foreach (var plan in activeImplementedPlans)
        {
            bool isProposer = plan.TeamId == teamId;
            bool isJoined = plan.Judgments.Any(j => j.TeamId == teamId && j.Judgment == PlanJudgmentType.Join);

            if (isProposer || isJoined)
            {
                var cost = await _costCalculationService.CalculatePlanCostAsync(plan.Id);
                int participantCount = 1 + plan.Judgments.Count(j => j.Judgment == PlanJudgmentType.Join);
                totalMonthlyExpenseBurden += (cost.TotalMonthlyExpensePoints / participantCount);
            }
        }

        return Math.Round(totalMonthlyExpenseBurden, 1);
    }
}
