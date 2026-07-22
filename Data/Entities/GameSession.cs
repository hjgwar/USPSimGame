using USPSimGame.Data.Enums;

namespace USPSimGame.Data.Entities;

public class GameSession
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CenterLatLong { get; set; } = string.Empty;
    public int Zoom { get; set; } = 15;
    public int StartYear { get; set; } = 2026;
    public int CurrentMonth { get; set; } = 0;
    public GameState State { get; set; } = GameState.Setup;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
