namespace USPSimGame.Data.Entities;

public class Team
{
    public int Id { get; set; }
    public int GameSessionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
}
