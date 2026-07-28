namespace USPSimGame.Services.Presets;

public interface IPresetFileService
{
    Task<List<(string FilePath, string DisplayName)>> GetAvailablePresetsAsync(string subDirectory);
    Task<T?> LoadPresetAsync<T>(string filePath);
    Task<(bool Success, string? ErrorMessage, string SavedPath)> ExportPresetAsync<T>(string subDirectory, string presetName, T data);
    Task<(bool Success, string? ErrorMessage, T? Data)> ImportPresetAsync<T>(string filePath);
}
