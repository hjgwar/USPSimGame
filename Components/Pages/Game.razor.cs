using Microsoft.AspNetCore.Components;
using OpenLayers.Blazor;
using USPSimGame.Services;

namespace USPSimGame.Components.Pages;

public partial class Game : ComponentBase
{
    [Inject]
    public PlayerSessionState PlayerSessionState { get; set; } = default!;

    [Inject]
    public NavigationManager Navigation { get; set; } = default!;

    protected OpenStreetMap? map;

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender && !PlayerSessionState.IsConnected)
        {
            Navigation.NavigateTo("/");
        }
    }
}