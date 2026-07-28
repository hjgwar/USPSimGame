using Microsoft.AspNetCore.Components;
using OpenLayers.Blazor;

namespace USPSimGame.Components.Game;

public partial class GameBaseMap : ComponentBase
{
    [Parameter, EditorRequired]
    public Coordinate InitialCenter { get; set; } = default!;

    [Parameter]
    public double InitialZoom { get; set; } = 15;

    protected override bool ShouldRender()
    {
        // Prevent Blazor from re-rendering the OpenLayers map component after initial creation.
        // This ensures panning and zoom levels are never reset by Blazor UI state changes.
        return false;
    }
}
