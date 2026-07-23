using USPSimGame.Data.Entities;
using USPSimGame.Data.Models;

namespace USPSimGame.Services.Layers;

public interface IMapLayerService
{
    Task<List<MapLayerDefinition>> GetAvailableLayerDefinitionsAsync();
    Task<List<GameSessionMapLayer>> GetSessionLayersAsync(int gameSessionId);
    Task PreFetchAndSaveSessionLayersAsync(int gameSessionId, string centerLatLong, IEnumerable<int>? enabledLayerDefinitionIds = null);
    Task UpdateLayerDefinitionTagsAsync(int definitionId, string? translatorTags, string? simulatorTags);
    Task UpdateSessionLayerTagsAsync(int sessionLayerId, string? translatorTags, string? simulatorTags);
    Task ResetSessionLayerTagsToDefaultAsync(int sessionLayerId);
    Task AttachLayerToSessionAsync(int gameSessionId, int layerDefinitionId);
    Task RemoveLayerFromSessionAsync(int sessionLayerId);
    LayerLegendInfo GetLegendForProvider(string providerKey);
}
