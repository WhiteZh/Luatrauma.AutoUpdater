using System.Text.Json;
using System.Text.Json.Serialization;

namespace Luatrauma.AutoUpdater;

using System;
using System.IO;

public record LocalVersionInfo(
    [property: JsonPropertyName("updated_at")]
    string UpdatedAt
)
{
    public const string DefaultFileName = "Luatrauma.AutoUpdater.LocalVersionInfo.json";

    public static async Task<LocalVersionInfo?> RetrieveLocalVersionInfoAsync(string workingDirectory)
    {
        try
        {
            var configFilePath = Path.Combine(workingDirectory, DefaultFileName);
            var configFileStream = new FileStream(configFilePath, FileMode.Open);

            var versionInfo = await JsonSerializer.DeserializeAsync<LocalVersionInfo>(configFileStream);

            if (versionInfo == null)
            {
                Logger.Log($"Failed to deserialize {DefaultFileName}", ConsoleColor.Red);
            }

            return versionInfo;
        }
        catch (UnauthorizedAccessException e)
        {
            Logger.Log($"Failed to retrieve local version info: {e.Message}");
            return null;
        }
        catch (FileNotFoundException e)
        {
            Logger.Log($"Failed to retrieve local version info: {e.Message}");
            return null;
        }
    }

    public static Task<LocalVersionInfo?> RetrieveLocalVersionInfoAsync()
    {
        return RetrieveLocalVersionInfoAsync(Directory.GetCurrentDirectory());
    }

    /**
     * Note: may throw
     */
    public async Task WriteToFileAsync(string filePath)
    {
        var jsonContent = JsonSerializer.Serialize(this);

        await File.WriteAllTextAsync(filePath, jsonContent);
    }

    /**
     * Note: may throw
     */
    public Task WriteToFileAsync()
    {
        return WriteToFileAsync(Path.Combine(Directory.GetCurrentDirectory(), DefaultFileName));
    }
}