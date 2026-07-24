using USPSimGame.Data.Entities;

namespace USPSimGame.Services.Plans;

public class PlanFeaturePayload
{
    public int GameSessionPlannableLayerId { get; set; }
    public string? GeoJsonGeometry { get; set; }
    public string? PropertiesJson { get; set; }
}

public interface IPlanService
{
    event Func<int, Plan, Task>? OnPlanCreated;
    event Func<int, Task>? OnPlanLockChanged;

    Task<List<Plan>> GetSessionPlansAsync(int gameSessionId, int currentTeamId = 0);
    Task<Plan?> GetPlanDetailsAsync(int planId);
    Task<Plan> CreatePlanAsync(int gameSessionId, int teamId, string name, string? description, int startMonth, List<PlanFeaturePayload> features);
    Task<Plan> UpdatePlanAsync(int planId, string name, string? description, int startMonth, List<PlanFeaturePayload> features);
    Task UpdatePlanStateAsync(int planId, PlanState newState);

    Task<(bool Success, string? ErrorMessage)> TryLockPlanAsync(int planId, int playerSessionId);
    Task UnlockPlanAsync(int planId);
}
