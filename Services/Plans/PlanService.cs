using Microsoft.EntityFrameworkCore;
using USPSimGame.Data;
using USPSimGame.Data.Entities;

namespace USPSimGame.Services.Plans;

public class PlanService : IPlanService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<PlanService> _logger;

    public event Func<int, Plan, Task>? OnPlanCreated;
    public event Func<int, Task>? OnPlanLockChanged;
    public event Func<int, Task>? OnPlanJudgmentsUpdated;

    public PlanService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<PlanService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<List<Plan>> GetSessionPlansAsync(int gameSessionId, int currentTeamId = 0)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var query = context.Plans
            .Include(p => p.Team)
            .Include(p => p.Features)
                .ThenInclude(f => f.GameSessionPlannableLayer)
                    .ThenInclude(gpl => gpl.PlannableLayerDefinition)
            .Where(p => p.GameSessionId == gameSessionId);

        if (currentTeamId > 0)
        {
            query = query.Where(p => p.State != PlanState.Draft || p.TeamId == currentTeamId);
        }

        var plans = await query.ToListAsync();

        // Populate LockedByUserName if locked
        var lockedSessionIds = plans
            .Where(p => !string.IsNullOrEmpty(p.LockedBySessionId) && int.TryParse(p.LockedBySessionId, out _))
            .Select(p => int.Parse(p.LockedBySessionId!))
            .Distinct()
            .ToList();

        if (lockedSessionIds.Any())
        {
            var playerSessions = await context.PlayerSessions
                .Where(ps => lockedSessionIds.Contains(ps.Id))
                .ToDictionaryAsync(ps => ps.Id.ToString(), ps => ps.UserName);

            foreach (var plan in plans)
            {
                if (!string.IsNullOrEmpty(plan.LockedBySessionId) && playerSessions.TryGetValue(plan.LockedBySessionId, out var userName))
                {
                    plan.LockedByUserName = userName;
                }
            }
        }

        return plans
            .OrderBy(p => GetStatePriority(p.State))
            .ThenBy(p => p.StartMonth)
            .ThenByDescending(p => p.CreatedAt)
            .ToList();
    }

    public async Task<Plan?> GetPlanDetailsAsync(int planId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var plan = await context.Plans
            .Include(p => p.Team)
            .Include(p => p.Features)
                .ThenInclude(f => f.GameSessionPlannableLayer)
                    .ThenInclude(gpl => gpl.PlannableLayerDefinition)
            .FirstOrDefaultAsync(p => p.Id == planId);

        if (plan != null && !string.IsNullOrEmpty(plan.LockedBySessionId) && int.TryParse(plan.LockedBySessionId, out int playerSessionId))
        {
            var ps = await context.PlayerSessions.FindAsync(playerSessionId);
            if (ps != null)
            {
                plan.LockedByUserName = ps.UserName;
            }
        }

        return plan;
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
        _logger.LogInformation("PlanService: Created Plan #{PlanId} '{Name}' for Session #{SessionId}", plan.Id, plan.Name, gameSessionId);

        var createdPlan = (await GetPlanDetailsAsync(plan.Id))!;

        if (OnPlanCreated != null)
        {
            try
            {
                await OnPlanCreated.Invoke(gameSessionId, createdPlan);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PlanService: Exception invoking OnPlanCreated subscribers.");
            }
        }

        return createdPlan;
    }

    public async Task<Plan> UpdatePlanAsync(
        int planId,
        string name,
        string? description,
        int startMonth,
        List<PlanFeaturePayload> features)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var plan = await context.Plans
            .Include(p => p.Features)
            .FirstOrDefaultAsync(p => p.Id == planId);

        if (plan == null)
        {
            throw new InvalidOperationException($"Plan #{planId} not found.");
        }

        plan.Name = name;
        plan.Description = description;
        plan.StartMonth = startMonth;
        plan.UpdatedAt = DateTime.UtcNow;

        // Remove old features and replace with updated feature payloads
        context.PlanFeatures.RemoveRange(plan.Features);

        foreach (var featPayload in features)
        {
            if (!string.IsNullOrWhiteSpace(featPayload.GeoJsonGeometry))
            {
                context.PlanFeatures.Add(new PlanFeature
                {
                    PlanId = plan.Id,
                    GameSessionPlannableLayerId = featPayload.GameSessionPlannableLayerId,
                    GeoJsonGeometry = featPayload.GeoJsonGeometry,
                    PropertiesJson = featPayload.PropertiesJson
                });
            }
        }

        await context.SaveChangesAsync();
        _logger.LogInformation("PlanService: Updated Plan #{PlanId} '{Name}' details and features.", plan.Id, plan.Name);

        var updatedPlan = (await GetPlanDetailsAsync(plan.Id))!;

        if (OnPlanCreated != null)
        {
            try
            {
                await OnPlanCreated.Invoke(updatedPlan.GameSessionId, updatedPlan);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PlanService: Exception invoking OnPlanCreated subscribers on update.");
            }
        }

        return updatedPlan;
    }

    public async Task UpdatePlanStateAsync(int planId, PlanState newState)
    {
        if (newState == PlanState.Implemented)
        {
            _logger.LogWarning("PlanService: Implemented state cannot be set manually by a player.");
            return;
        }

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var plan = await context.Plans.FindAsync(planId);
        if (plan != null && plan.State != PlanState.Implemented)
        {
            plan.State = newState;
            plan.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
            _logger.LogInformation("PlanService: Updated Plan #{PlanId} state to {State}", planId, newState);

            if (newState == PlanState.Draft)
            {
                await ResetPlanJudgmentsAsync(planId);
            }

            if (OnPlanLockChanged != null)
            {
                try { await OnPlanLockChanged.Invoke(plan.GameSessionId); } catch { }
            }
        }
    }

    public async Task<List<PlanTeamJudgment>> GetPlanJudgmentsAsync(int planId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        return await context.PlanTeamJudgments
            .Include(j => j.Team)
            .Where(j => j.PlanId == planId)
            .ToListAsync();
    }

    public async Task SubmitTeamJudgmentAsync(int planId, int teamId, PlanJudgmentType judgment)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var plan = await context.Plans.FindAsync(planId);
        if (plan == null) return;

        var existing = await context.PlanTeamJudgments
            .FirstOrDefaultAsync(j => j.PlanId == planId && j.TeamId == teamId);

        if (existing == null)
        {
            context.PlanTeamJudgments.Add(new PlanTeamJudgment
            {
                PlanId = planId,
                TeamId = teamId,
                Judgment = judgment,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Judgment = judgment;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
        _logger.LogInformation("PlanService: Team #{TeamId} submitted judgment '{Judgment}' for Plan #{PlanId}", teamId, judgment, planId);

        if (OnPlanJudgmentsUpdated != null)
        {
            try { await OnPlanJudgmentsUpdated.Invoke(plan.GameSessionId); } catch { }
        }
    }

    public async Task ResetPlanJudgmentsAsync(int planId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var plan = await context.Plans.FindAsync(planId);
        if (plan == null) return;

        var existingList = await context.PlanTeamJudgments
            .Where(j => j.PlanId == planId)
            .ToListAsync();

        if (existingList.Any())
        {
            context.PlanTeamJudgments.RemoveRange(existingList);
            await context.SaveChangesAsync();
            _logger.LogInformation("PlanService: Reset judgments for Plan #{PlanId}", planId);

            if (OnPlanJudgmentsUpdated != null)
            {
                try { await OnPlanJudgmentsUpdated.Invoke(plan.GameSessionId); } catch { }
            }
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> TryLockPlanAsync(int planId, int playerSessionId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var plan = await context.Plans.FindAsync(planId);

        if (plan == null)
        {
            return (false, "Plan not found.");
        }

        string sessionStr = playerSessionId.ToString();

        // Check if already locked by someone else
        if (!string.IsNullOrEmpty(plan.LockedBySessionId) && plan.LockedBySessionId != sessionStr)
        {
            var lockingPlayer = await context.PlayerSessions.FindAsync(int.Parse(plan.LockedBySessionId));
            string name = lockingPlayer?.UserName ?? "another player";
            return (false, $"Plan is currently locked and being edited by {name}.");
        }

        plan.LockedBySessionId = sessionStr;
        plan.LockedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        _logger.LogInformation("PlanService: Locked Plan #{PlanId} for PlayerSession #{SessionId}", planId, playerSessionId);

        if (OnPlanLockChanged != null)
        {
            try
            {
                await OnPlanLockChanged.Invoke(plan.GameSessionId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PlanService: Exception invoking OnPlanLockChanged subscribers.");
            }
        }

        return (true, null);
    }

    public async Task UnlockPlanAsync(int planId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var plan = await context.Plans.FindAsync(planId);

        if (plan != null && !string.IsNullOrEmpty(plan.LockedBySessionId))
        {
            int sessionId = plan.GameSessionId;
            plan.LockedBySessionId = null;
            plan.LockedAt = null;
            await context.SaveChangesAsync();

            _logger.LogInformation("PlanService: Unlocked Plan #{PlanId}", planId);

            if (OnPlanLockChanged != null)
            {
                try
                {
                    await OnPlanLockChanged.Invoke(sessionId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "PlanService: Exception invoking OnPlanLockChanged subscribers.");
                }
            }
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
