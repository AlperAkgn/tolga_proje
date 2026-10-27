using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Afterform.Api.Uploads;

public sealed partial class LocalDemoFileStorage : IDemoFileStorage
{
    private readonly string root;
    private readonly string pending;

    public LocalDemoFileStorage(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configured = configuration["DemoStorage:RootPath"];
        if (string.IsNullOrWhiteSpace(configured) && !environment.IsDevelopment())
            throw new InvalidOperationException("Configure DemoStorage:RootPath outside the app and web directories.");

        root = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "../../.local/uploads/demos")
            : Path.IsPathFullyQualified(configured)
                ? configured : Path.Combine(environment.ContentRootPath, configured));

        string[] forbidden = [environment.ContentRootPath,
            environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"),
            Path.GetFullPath(Path.Combine(environment.ContentRootPath, "../../outputs"))];
        if (forbidden.Any(path => IsWithin(root, path) || IsWithin(path, root)))
            throw new InvalidOperationException("Demo storage must be separate from the app and public frontend directories.");

        pending = Path.Combine(root, ".pending");
        Directory.CreateDirectory(pending);
    }

    public async Task<StoredDemoFile> SaveAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length <= 0 || file.Length > DemoUploadRules.MaxFileBytes)
            throw new DemoFileValidationException("Choose a non-empty audio file no larger than 100 MB.");

        // An original name is display metadata only, never a storage path.
        var original = Path.GetFileName(file.FileName.Replace('\\', '/')).Trim();
        if (original.Length is < 1 or > 255
            || original.Any(character => char.IsControl(character)
                || CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.Format))
            throw new DemoFileValidationException("The audio file name is invalid or longer than 255 characters.");

        var extension = Path.GetExtension(original).ToLowerInvariant();
        if (!DemoUploadRules.Extensions.Contains(extension, StringComparer.Ordinal))
            throw new DemoFileValidationException("Choose an MP3, WAV or FLAC audio file.");

        var key = Guid.NewGuid().ToString("N") + extension;
        var stagedPath = Path.Combine(pending, key + ".part");
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            long total = 0;
            string sha256;
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                await using var input = file.OpenReadStream();
                await using var output = new FileStream(stagedPath, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous);
                int read;
                while ((read = await input.ReadAsync(buffer.AsMemory(), cancellationToken)) != 0)
                {
                    total += read;
                    if (total > DemoUploadRules.MaxFileBytes)
                        throw new DemoFileValidationException("The audio file exceeds the 100 MB limit.");
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
                if (total == 0 || total != file.Length)
                    throw new DemoFileValidationException("The audio upload was incomplete. Choose the original file again.");
                sha256 = Convert.ToHexStringLower(hash.GetHashAndReset());
            }

            // Inspect file structure, not the browser's untrusted Content-Type.
            var contentType = AudioFileInspector.Inspect(stagedPath, extension, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(stagedPath, Resolve(key), overwrite: false);
            return new StoredDemoFile(key, original, contentType, total, sha256);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            if (File.Exists(stagedPath)) File.Delete(stagedPath);
        }
    }

    public Stream? OpenRead(string storageKey)
    {
        var path = Resolve(storageKey);
        try { return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            64 * 1024, FileOptions.Asynchronous); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    public void Delete(string storageKey) => File.Delete(Resolve(storageKey));

    private string Resolve(string storageKey)
    {
        if (!StorageKeyPattern().IsMatch(storageKey))
            throw new InvalidDataException("Invalid private demo storage identifier.");
        return Path.Combine(root, storageKey);
    }

    private static bool IsWithin(string candidate, string parent)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(parent), candidate);
        return !Path.IsPathRooted(relative) && relative != ".."
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    [GeneratedRegex("\\A[a-f0-9]{32}\\.(mp3|wav|flac)\\z", RegexOptions.CultureInvariant)]
    private static partial Regex StorageKeyPattern();
}
