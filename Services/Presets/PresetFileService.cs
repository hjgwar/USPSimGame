using System.Text.Json;

namespace USPSimGame.Services.Presets;

public class PresetFileService : IPresetFileService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<PresetFileService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public PresetFileService(IWebHostEnvironment env, ILogger<PresetFileService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public Task<List<(string FilePath, string DisplayName)>> GetAvailablePresetsAsync(string subDirectory)
    {
        var result = new List<(string FilePath, string DisplayName)>();
        string dirPath = Path.Combine(_env.WebRootPath, "presets", subDirectory);
        if (!Directory.Exists(dirPath)) return Task.FromResult(result);

        foreach (var file in Directory.GetFiles(dirPath, "*.json"))
        {
            string baseName = Path.GetFileNameWithoutExtension(file);
            string displayName = baseName.Replace('_', ' ');
            result.Add((file, displayName));
        }

        return Task.FromResult(result);
    }

    public async Task<T?> LoadPresetAsync<T>(string filePath)
    {
        if (!File.Exists(filePath)) return default;
        try
        {
            string json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<T>(json, JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PresetFileService] Error reading preset '{FilePath}'", filePath);
            return default;
        }
    }

    public async Task<(bool Success, string? ErrorMessage, string SavedPath)> ExportPresetAsync<T>(string subDirectory, string presetName, T data)
    {
        try
        {
            string dirPath = Path.Combine(_env.WebRootPath, "presets", subDirectory);
            if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);

            string safeFileName = string.Join("_", presetName.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_") + ".json";
            string fullPath = Path.Combine(dirPath, safeFileName);

            string json = JsonSerializer.Serialize(data, JsonOpts);
            await File.WriteAllTextAsync(fullPath, json);

            return (true, null, fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PresetFileService] Error exporting preset '{PresetName}'", presetName);
            return (false, ex.Message, string.Empty);
        }
    }

    public async Task<(bool Success, string? ErrorMessage, T? Data)> ImportPresetAsync<T>(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return (false, $"File not found: '{filePath}'", default);
        }

        try
        {
            string json = await File.ReadAllTextAsync(filePath);
            var data = JsonSerializer.Deserialize<T>(json, JsonOpts);
            if (data == null) return (false, "Failed to parse JSON file.", default);
            return (true, null, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PresetFileService] Error importing preset file '{FilePath}'", filePath);
            return (false, ex.Message, default);
        }
    }
}
