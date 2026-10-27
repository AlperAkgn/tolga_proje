using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;
using Afterform.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace Afterform.Api.Security;

public static partial class ArtistProvisioner
{
    // A local CLI command, never exposed as a registration HTTP endpoint.
    // The password arrives on standard input, not in process arguments or logs.
    public static async Task<int> RunAsync(IServiceProvider services)
    {
        try
        {
            using var input = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
            var payload = await input.ReadLineAsync();
            if (payload is null || payload.Length > 4096)
                throw new ProvisioningException("Expected artist account details on standard input.");
            var request = JsonSerializer.Deserialize<ProvisionRequest>(payload,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (request is null) throw new ProvisioningException("Artist details are missing.");
            var userName = request.UserName?.Trim() ?? string.Empty;
            var displayName = request.DisplayName?.Trim() ?? string.Empty;
            if (!UserNamePattern().IsMatch(userName))
                throw new ProvisioningException("Username must be 1-64 characters: letters, digits, - . _ @ +.");
            if (displayName.Length is < 1 or > 120)
                throw new ProvisioningException("Artist display name must be 1-120 characters.");
            if (request.Password?.Length is < 15 or > 128 || string.IsNullOrWhiteSpace(request.Password))
                throw new ProvisioningException("Password must contain 15-128 characters and cannot be only whitespace.");

            using var scope = services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ArtistUser>>();
            if (await users.FindByNameAsync(userName) is not null)
                throw new ProvisioningException("This username already exists. Its account and password were not changed.");

            var artist = new ArtistUser
            {
                UserName = userName,
                DisplayName = displayName,
                LockoutEnabled = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            var result = await users.CreateAsync(artist, request.Password);
            if (!result.Succeeded)
                throw new ProvisioningException(string.Join(" ", result.Errors.Select(error => error.Description)));
            Console.WriteLine($"Artist account created: {artist.UserName} / {artist.DisplayName}.");
            Console.WriteLine("Password hashed by Identity with a new random salt; plaintext password was not stored.");
            return 0;
        }
        catch (Exception exception)
        {
            // A database error can contain SQL; print only a known safe account error.
            var message = exception is ProvisioningException or JsonException
                ? exception is JsonException ? "Artist details are not valid JSON." : exception.Message
                : "Artist account could not be created. Check the database connection and applied migrations.";
            Console.Error.WriteLine(message);
            return 1;
        }
    }

    [GeneratedRegex(@"^[a-zA-Z0-9._@+\-]{1,64}$")]
    private static partial Regex UserNamePattern();

    private sealed record ProvisionRequest(string? UserName, string? DisplayName, string? Password);

    private sealed class ProvisioningException(string message) : Exception(message);
}
