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

    Task<List<Plan>> GetSessionPlansAsync(int gameSessionId);
    Task<Plan?> GetPlanDetailsAsync(int planId);
    Task<Plan> CreatePlanAsync(int gameSessionId, int teamId, string name, string? description, int startMonth, List<PlanFeaturePayload> features);
    Task UpdatePlanStateAsync(int planId, PlanState newState);
}
