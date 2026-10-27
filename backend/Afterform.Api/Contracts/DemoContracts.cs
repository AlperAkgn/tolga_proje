using System.ComponentModel.DataAnnotations;
using Afterform.Api.Models;

namespace Afterform.Api.Contracts;

public sealed class DemoUploadRequest
{
    [Required, StringLength(100)]
    public string TrackTitle { get; init; } = string.Empty;

    [Required, StringLength(64)]
    public string Genre { get; init; } = string.Empty;

    [StringLength(500)]
    public string? ArtistNote { get; init; }

    [Required]
    public IFormFile? AudioFile { get; init; }

    public bool RightsConfirmation { get; init; }
}

public sealed record DemoResponse(
    long Id, string ArtistId, string Title, string Genre, string? Description, string Status,
    DateTime SubmittedAtUtc, DateTime? UpdatedAtUtc, string OriginalFileName,
    string ContentType, long FileSizeBytes, string FileSha256,
    string AudioUrl, string DownloadUrl)
{
    public static DemoResponse From(DemoSubmission demo) => new(
        demo.Id, demo.ArtistId, demo.Title, demo.Genre, demo.Description, demo.Status.ToString(),
        DateTime.SpecifyKind(demo.SubmittedAtUtc, DateTimeKind.Utc),
        demo.UpdatedAtUtc is { } updated ? DateTime.SpecifyKind(updated, DateTimeKind.Utc) : null,
        demo.OriginalFileName,
        demo.ContentType, demo.FileSizeBytes, demo.FileSha256,
        $"/api/demos/{demo.Id}/audio", $"/api/demos/{demo.Id}/audio?download=true");
}

public sealed class DemoHistoryQuery
{
    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 50)]
    public int PageSize { get; init; } = 10;

    [StringLength(24)]
    public string? Status { get; init; }

    [StringLength(100)]
    public string? Search { get; init; }
}

public sealed record DemoStatusCounts(int Total, int Submitted, int InReview, int Accepted, int Rejected);

public sealed record DemoHistoryResponse(
    string ArtistId, IReadOnlyList<DemoResponse> Items, int Page, int PageSize,
    int TotalCount, int TotalPages, DemoStatusCounts Summary);
