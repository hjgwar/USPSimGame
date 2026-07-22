namespace USPSimGame.Data.Entities;

public class GameSession
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CenterLatLong { get; set; } = string.Empty;
    public int Zoom { get; set; }
    public int StartYear { get; set; }
    public int CurrentMonth { get; set; }
    public string State { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
