using Microsoft.EntityFrameworkCore;
using USPSimGame.Data;
using USPSimGame.Data.Entities;
using USPSimGame.Data.Models;

namespace USPSimGame.Services.Layers;

public class MapLayerService : IMapLayerService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IEnumerable<IMapLayerProvider> _layerProviders;
    private readonly ILogger<MapLayerService> _logger;

    public MapLayerService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IEnumerable<IMapLayerProvider> layerProviders,
        ILogger<MapLayerService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _layerProviders = layerProviders;
        _logger = logger;
    }

    public async Task<List<MapLayerDefinition>> GetAvailableLayerDefinitionsAsync()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        return await context.MapLayerDefinitions
            .OrderBy(d => d.Category)
            .ThenBy(d => d.Name)
            .ToListAsync();
    }

    public async Task<List<GameSessionMapLayer>> GetSessionLayersAsync(int gameSessionId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        return await context.GameSessionMapLayers
            .Include(l => l.LayerDefinition)
            .Where(l => l.GameSessionId == gameSessionId && l.IsEnabled)
            .ToListAsync();
    }

    public async Task PreFetchAndSaveSessionLayersAsync(int gameSessionId, string centerLatLong, IEnumerable<int>? enabledLayerDefinitionIds = null)
    {
        _logger.LogInformation("MapLayerService: PreFetchAndSaveSessionLayersAsync for GameSessionId {GameSessionId}, CenterLatLong '{Center}'", gameSessionId, centerLatLong);

        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var allDefinitions = await context.MapLayerDefinitions.ToListAsync();
        var selectedIds = enabledLayerDefinitionIds?.ToList();

        var targetDefinitions = allDefinitions
            .Where(d => selectedIds == null ? d.IsEnabledByDefault : selectedIds.Contains(d.Id))
            .ToList();

        foreach (var def in targetDefinitions)
        {
            await ProcessAndSaveLayerForSessionAsync(context, gameSessionId, centerLatLong, def);
        }

        await context.SaveChangesAsync();
        _logger.LogInformation("MapLayerService: Finished pre-fetching and saving session layers.");
    }

    public async Task AttachLayerToSessionAsync(int gameSessionId, int layerDefinitionId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var session = await context.GameSessions.FindAsync(gameSessionId);
        var def = await context.MapLayerDefinitions.FindAsync(layerDefinitionId);

        if (session != null && def != null)
        {
            _logger.LogInformation("MapLayerService: Attaching optional layer '{Name}' to GameSession #{SessionId}...", def.Name, gameSessionId);
            await ProcessAndSaveLayerForSessionAsync(context, gameSessionId, session.CenterLatLong, def);
            await context.SaveChangesAsync();
        }
    }

    public async Task RemoveLayerFromSessionAsync(int sessionLayerId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var sessionLayer = await context.GameSessionMapLayers.FindAsync(sessionLayerId);
        if (sessionLayer != null)
        {
            context.GameSessionMapLayers.Remove(sessionLayer);
            await context.SaveChangesAsync();
        }
    }

    public async Task UpdateLayerDefinitionTagsAsync(int definitionId, string? translatorTags, string? simulatorTags)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var def = await context.MapLayerDefinitions.FindAsync(definitionId);
        if (def != null)
        {
            def.TranslatorTags = translatorTags;
            def.SimulatorTags = simulatorTags;
            await context.SaveChangesAsync();
        }
    }

    public async Task UpdateSessionLayerTagsAsync(int sessionLayerId, string? translatorTags, string? simulatorTags)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var sessionLayer = await context.GameSessionMapLayers.FindAsync(sessionLayerId);
        if (sessionLayer != null)
        {
            sessionLayer.TranslatorTags = translatorTags;
            sessionLayer.SimulatorTags = simulatorTags;
            await context.SaveChangesAsync();
        }
    }

    public async Task ResetSessionLayerTagsToDefaultAsync(int sessionLayerId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var sessionLayer = await context.GameSessionMapLayers
            .Include(l => l.LayerDefinition)
            .FirstOrDefaultAsync(l => l.Id == sessionLayerId);

        if (sessionLayer != null && sessionLayer.LayerDefinition != null)
        {
            sessionLayer.TranslatorTags = sessionLayer.LayerDefinition.TranslatorTags;
            sessionLayer.SimulatorTags = sessionLayer.LayerDefinition.SimulatorTags;
            await context.SaveChangesAsync();
        }
    }

    private async Task ProcessAndSaveLayerForSessionAsync(AppDbContext context, int gameSessionId, string centerLatLong, MapLayerDefinition def)
    {
        var existingLayer = await context.GameSessionMapLayers
            .FirstOrDefaultAsync(l => l.GameSessionId == gameSessionId && l.MapLayerDefinitionId == def.Id);

        if (existingLayer == null)
        {
            existingLayer = new GameSessionMapLayer
            {
                GameSessionId = gameSessionId,
                MapLayerDefinitionId = def.Id,
                IsEnabled = true,
                TranslatorTags = def.TranslatorTags,
                SimulatorTags = def.SimulatorTags
            };
            context.GameSessionMapLayers.Add(existingLayer);
        }

        var provider = _layerProviders.FirstOrDefault(p => p.ProviderKey.Equals(def.Key, StringComparison.OrdinalIgnoreCase));
        if (provider != null)
        {
            try
            {
                _logger.LogInformation("MapLayerService: Invoking provider '{Key}' to fetch layer data...", provider.ProviderKey);
                var cachedContent = await provider.FetchLayerDataAsync(centerLatLong, 1.0);

                if (!string.IsNullOrEmpty(cachedContent))
                {
                    existingLayer.CachedDataContent = cachedContent;
                    existingLayer.LastFetchedAt = DateTime.UtcNow;
                    _logger.LogInformation("MapLayerService: Successfully pre-cached {Length} bytes for layer '{Name}'.", cachedContent.Length, def.Name);
                }
                else
                {
                    _logger.LogWarning("MapLayerService: Provider '{Key}' returned null or empty content.", provider.ProviderKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MapLayerService: Error pre-fetching data for layer '{Key}'", def.Key);
            }
        }
        else
        {
            _logger.LogWarning("MapLayerService: No registered IMapLayerProvider found for Key '{Key}'.", def.Key);
        }
    }

    public LayerLegendInfo GetLegendForProvider(string providerKey)
    {
        var provider = _layerProviders.FirstOrDefault(p => p.ProviderKey.Equals(providerKey, StringComparison.OrdinalIgnoreCase));
        if (provider != null)
        {
            return provider.GetLegendInfo();
        }

        return new LayerLegendInfo();
    }
}
