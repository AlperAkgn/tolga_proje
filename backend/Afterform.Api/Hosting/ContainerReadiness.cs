using System.Text.Json;

namespace Afterform.Api.Hosting;

public static class ContainerReadiness
{
    public static async Task<int> RunAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        try
        {
            using var response = await client.GetAsync("http://127.0.0.1:8080/api/health/ready");
            if (!response.IsSuccessStatusCode) return 1;
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = body.RootElement;
            return root.GetProperty("status").GetString() == "ready"
                && root.GetProperty("database").GetString() == "connected"
                && root.GetProperty("schema").GetString() == "upToDate" ? 0 : 1;
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException or JsonException or KeyNotFoundException
            or InvalidOperationException)
        {
            // Health output contains no response bodies, connection strings or tokens.
            Console.Error.WriteLine("Afterform readiness is unavailable.");
            return 1;
        }
    }
}
