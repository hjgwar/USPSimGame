using USPSimGame.Data.Enums;

namespace USPSimGame.Data.Entities;

public class GameSession
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CenterLatLong { get; set; } = string.Empty;
    public int Zoom { get; set; } = 16;
    public int StartYear { get; set; } = 2026;
    public int CurrentMonth { get; set; } = 0;
    public GameState State { get; set; } = GameState.Setup;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<GameSessionMapLayer> MapLayers { get; set; } = new List<GameSessionMapLayer>();
    public ICollection<Team> Teams { get; set; } = new List<Team>();
    public ICollection<GameSessionPlannableLayer> PlannableLayers { get; set; } = new List<GameSessionPlannableLayer>();
    public ICollection<Plan> Plans { get; set; } = new List<Plan>();
}
