using System.ComponentModel.DataAnnotations.Schema;

namespace USPSimGame.Data.Entities;

public class Plan
{
    public int Id { get; set; }

    public int GameSessionId { get; set; }

    [ForeignKey(nameof(GameSessionId))]
    public GameSession GameSession { get; set; } = default!;

    public int TeamId { get; set; }

    [ForeignKey(nameof(TeamId))]
    public Team Team { get; set; } = default!;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int StartMonth { get; set; } = 0;

    public PlanState State { get; set; } = PlanState.Draft;

    public string? LockedBySessionId { get; set; }

    [NotMapped]
    public string? LockedByUserName { get; set; }

    public DateTime? LockedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PlanFeature> Features { get; set; } = new List<PlanFeature>();
}
