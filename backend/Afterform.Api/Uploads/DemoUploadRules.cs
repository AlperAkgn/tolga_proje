namespace Afterform.Api.Uploads;

public static class DemoUploadRules
{
    public const long MaxFileBytes = 100L * 1024 * 1024;
    public const long MaxRequestBytes = MaxFileBytes + 256 * 1024;
    public const string UploadRatePolicy = "artist-demo-upload";

    public static readonly string[] Extensions = [".mp3", ".wav", ".flac"];
    public static readonly HashSet<string> Genres = new(StringComparer.Ordinal)
    {
        "Alternative pop", "Electronic", "Indie rock", "R&B / Soul", "Experimental", "Other"
    };
}

public sealed class DemoFileValidationException(string message) : Exception(message);

public sealed record StoredDemoFile(
    string StorageKey, string OriginalFileName, string ContentType,
    long SizeBytes, string Sha256);

// The storage identifier is internal; callers never receive an absolute path.
public interface IDemoFileStorage
{
    Task<StoredDemoFile> SaveAsync(IFormFile file, CancellationToken cancellationToken);
    Stream? OpenRead(string storageKey);
    void Delete(string storageKey);
}
