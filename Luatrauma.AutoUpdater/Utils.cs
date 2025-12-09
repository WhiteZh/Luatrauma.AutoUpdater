namespace Luatrauma.AutoUpdater;

public static class Utils
{
    public static HttpClient CreateProductSpecificHttpClient()
    {
        var httpClient = new HttpClient();
        
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Luatrauma.AutoUpdater/1.0");

        return httpClient;
    }
}