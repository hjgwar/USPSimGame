using Microsoft.EntityFrameworkCore;
using USPSimGame.Data;
using USPSimGame.Data.Entities;

namespace USPSimGame.Services.Plans;

public class PlanService : IPlanService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<PlanService> _logger;

    public event Func<int, Plan, Task>? OnPlanCreated;

    public PlanService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<PlanService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<List<Plan>> GetSessionPlansAsync(int gameSessionId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var plans = await context.Plans
            .Include(p => p.Team)
            .Include(p => p.Features)
                .ThenInclude(f => f.GameSessionPlannableLayer)
                    .ThenInclude(gpl => gpl.PlannableLayerDefinition)
            .Where(p => p.GameSessionId == gameSessionId)
            .ToListAsync();

        return plans
            .OrderBy(p => GetStatePriority(p.State))
            .ThenBy(p => p.StartMonth)
            .ThenByDescending(p => p.CreatedAt)
            .ToList();
    }

    public async Task<Plan?> GetPlanDetailsAsync(int planId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        return await context.Plans
            .Include(p => p.Team)
            .Include(p => p.Features)
                .ThenInclude(f => f.GameSessionPlannableLayer)
                    .ThenInclude(gpl => gpl.PlannableLayerDefinition)
            .FirstOrDefaultAsync(p => p.Id == planId);
    }

    public async Task<Plan> CreatePlanAsync(
        int gameSessionId,
        int teamId,
        string name,
        string? description,
        int startMonth,
        List<PlanFeaturePayload> features)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var plan = new Plan
        {
            GameSessionId = gameSessionId,
            TeamId = teamId,
            Name = name,
            Description = description,
            StartMonth = startMonth,
            State = PlanState.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Plans.Add(plan);
        await context.SaveChangesAsync();

        foreach (var featPayload in features)
        {
            if (!string.IsNullOrWhiteSpace(featPayload.GeoJsonGeometry))
            {
                var feature = new PlanFeature
                {
                    PlanId = plan.Id,
                    GameSessionPlannableLayerId = featPayload.GameSessionPlannableLayerId,
                    GeoJsonGeometry = featPayload.GeoJsonGeometry,
                    PropertiesJson = featPayload.PropertiesJson
                };

                context.PlanFeatures.Add(feature);
            }
        }

        await context.SaveChangesAsync();

        _logger.LogInformation("PlanService: Created Plan #{PlanId} '{Name}' with {Count} features for Session #{SessionId}", plan.Id, plan.Name, features.Count, gameSessionId);

        var createdPlan = (await GetPlanDetailsAsync(plan.Id))!;

        if (OnPlanCreated != null)
        {
            try
            {
                await OnPlanCreated.Invoke(gameSessionId, createdPlan);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PlanService: Exception invoking OnPlanCreated event subscribers.");
            }
        }

        return createdPlan;
    }

    public async Task UpdatePlanStateAsync(int planId, PlanState newState)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var plan = await context.Plans.FindAsync(planId);
        if (plan != null)
        {
            plan.State = newState;
            plan.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
            _logger.LogInformation("PlanService: Updated Plan #{PlanId} state to {State}", planId, newState);
        }
    }

    private static int GetStatePriority(PlanState state)
    {
        return state switch
        {
            PlanState.Draft => 1,
            PlanState.Requested => 2,
            PlanState.Approved => 3,
            PlanState.Implemented => 4,
            PlanState.Archived => 5,
            _ => 99
        };
    }
}
