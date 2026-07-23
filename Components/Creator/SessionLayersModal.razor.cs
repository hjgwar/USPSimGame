using Microsoft.AspNetCore.Components;
using USPSimGame.Data.Entities;
using USPSimGame.Services.Layers;

namespace USPSimGame.Components.Creator;

public partial class SessionLayersModal : ComponentBase
{
    [Inject]
    public IMapLayerService MapLayerService { get; set; } = default!;

    [Parameter, EditorRequired]
    public GameSession Session { get; set; } = default!;

    [Parameter]
    public EventCallback OnClose { get; set; }

    [Parameter]
    public EventCallback OnUpdated { get; set; }

    protected List<GameSessionMapLayer> SessionLayers { get; set; } = new();
    protected List<MapLayerDefinition> AvailableCatalogDefinitions { get; set; } = new();
    protected int SelectedCatalogLayerId { get; set; } = 0;
    protected bool IsLoading { get; set; } = true;
    protected bool IsAttachingLayer { get; set; } = false;

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            SessionLayers = await MapLayerService.GetSessionLayersAsync(Session.Id);
            AvailableCatalogDefinitions = await MapLayerService.GetAvailableLayerDefinitionsAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SessionLayersModal] Error loading data: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected async Task AttachCatalogLayerAsync()
    {
        if (SelectedCatalogLayerId > 0)
        {
            IsAttachingLayer = true;
            try
            {
                await MapLayerService.AttachLayerToSessionAsync(Session.Id, SelectedCatalogLayerId);
                SelectedCatalogLayerId = 0;
                await LoadDataAsync();
                await OnUpdated.InvokeAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SessionLayersModal] Error attaching layer: {ex.Message}");
            }
            finally
            {
                IsAttachingLayer = false;
            }
        }
    }

    protected async Task RemoveSessionLayerAsync(int sessionLayerId)
    {
        try
        {
            await MapLayerService.RemoveLayerFromSessionAsync(sessionLayerId);
            await LoadDataAsync();
            await OnUpdated.InvokeAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SessionLayersModal] Error removing layer: {ex.Message}");
        }
    }

    protected async Task SaveSessionLayerTagsAsync(GameSessionMapLayer layer)
    {
        try
        {
            await MapLayerService.UpdateSessionLayerTagsAsync(layer.Id, layer.TranslatorTags, layer.SimulatorTags);
            await LoadDataAsync();
            await OnUpdated.InvokeAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SessionLayersModal] Error saving tags: {ex.Message}");
        }
    }

    protected async Task ResetSessionLayerTagsAsync(int sessionLayerId)
    {
        try
        {
            await MapLayerService.ResetSessionLayerTagsToDefaultAsync(sessionLayerId);
            await LoadDataAsync();
            await OnUpdated.InvokeAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SessionLayersModal] Error resetting tags: {ex.Message}");
        }
    }

    protected async Task CloseAsync()
    {
        await OnClose.InvokeAsync();
    }
}
