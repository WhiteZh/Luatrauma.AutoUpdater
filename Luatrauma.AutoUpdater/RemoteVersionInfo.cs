using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Luatrauma.AutoUpdater;

public record RemoteVersionInfo(
    [property: JsonPropertyName("updated_at")]
    string UpdatedAt
)
{
    /**
     * Note: may throw
     * <returns>null if deserialization failed</returns>
     */
    public static async Task<RemoteVersionInfo?> FetchRemoteVersionInfoAsync()
    {
        // you need to use product specific http client (with user-agent header), otherwise the request will be rejected
        using var httpClient = Utils.CreateProductSpecificHttpClient();

        var responseMessage = await httpClient.GetAsync(
            "https://api.github.com/repos/evilfactory/LuaCsForBarotrauma/releases/latest"
        );

        if (responseMessage.StatusCode == HttpStatusCode.OK)
        {
            RemoteVersionInfo? remoteVersionInfo;
            try
            {
                remoteVersionInfo = await JsonSerializer.DeserializeAsync<RemoteVersionInfo>(
                    await responseMessage.Content.ReadAsStreamAsync()
                );
            }
            catch (Exception e)
            {
                remoteVersionInfo = null;
            }

            if (remoteVersionInfo == null)
            {
                Logger.Log("Failed to deserialize GitHub API response", ConsoleColor.Red);
            }

            return remoteVersionInfo;
        }

        Logger.Log(
            $"GitHub API response is NOT OK:\n{responseMessage}\nwith content:\n{await responseMessage.Content.ReadAsStringAsync()}");

        if (responseMessage.StatusCode == HttpStatusCode.Forbidden)
        {
            Logger.Log("GitHub API responses 403; assume rate limit exceeded");

            return null;
        }

        Logger.Log(
            $"Encountered unexpected response status code: {responseMessage.StatusCode}; review is recommended",
            ConsoleColor.Yellow
        );
        throw new Exception($"Unexpected response status code: {responseMessage.StatusCode}");
    }
}