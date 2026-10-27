namespace Afterform.Api.Models;

public sealed class DemoSubmission
{
    public long Id { get; set; }
    public string ArtistId { get; set; } = string.Empty;
    public ArtistUser Artist { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DemoStatus Status { get; set; } = DemoStatus.Submitted;
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    // Store a private storage identifier, rather than the audio bytes or a public URL.
    public string StorageKey { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string FileSha256 { get; set; } = string.Empty;
    public DateTime? RightsConfirmedAtUtc { get; set; }
}
