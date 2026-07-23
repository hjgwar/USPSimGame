using Microsoft.AspNetCore.Components;
using USPSimGame.Data.Entities;

namespace USPSimGame.Components.Pages.Creator;

public partial class CreatorLayers : ComponentBase
{
    protected List<MapLayerDefinition> LayerDefinitions { get; set; } = new();
    protected bool IsLoading { get; set; } = true;

    protected bool ShowEditModal { get; set; } = false;
    protected MapLayerDefinition? EditingLayer { get; set; }
    protected string EditTranslatorTags { get; set; } = string.Empty;
    protected string EditSimulatorTags { get; set; } = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        if (AuthState.IsAuthenticated)
        {
            await LoadLayerDefinitionsAsync();
        }
        else
        {
            IsLoading = false;
        }
    }

    private async Task LoadLayerDefinitionsAsync()
    {
        IsLoading = true;
        try
        {
            LayerDefinitions = await MapLayerService.GetAvailableLayerDefinitionsAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CreatorLayers] Error loading layer definitions: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected void OpenEditModal(MapLayerDefinition layer)
    {
        EditingLayer = layer;
        EditTranslatorTags = layer.TranslatorTags ?? string.Empty;
        EditSimulatorTags = layer.SimulatorTags ?? string.Empty;
        ShowEditModal = true;
    }

    protected void CloseEditModal()
    {
        ShowEditModal = false;
        EditingLayer = null;
    }

    protected async Task SaveCatalogDefaultTagsAsync()
    {
        if (EditingLayer != null)
        {
            try
            {
                await MapLayerService.UpdateLayerDefinitionTagsAsync(EditingLayer.Id, EditTranslatorTags, EditSimulatorTags);
                await LoadLayerDefinitionsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CreatorLayers] Error saving catalog tags: {ex.Message}");
            }
        }
        CloseEditModal();
    }
}
