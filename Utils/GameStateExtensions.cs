using USPSimGame.Data.Enums;

namespace USPSimGame.Utils;

public static class GameStateExtensions
{
    public static string GetBadgeClass(this GameState state) => state switch
    {
        GameState.Setup => "bg-info text-dark",
        GameState.Play => "bg-success",
        GameState.Pause => "bg-warning text-dark",
        GameState.Simulation => "bg-primary",
        GameState.Complete => "bg-secondary",
        _ => "bg-secondary"
    };
}
