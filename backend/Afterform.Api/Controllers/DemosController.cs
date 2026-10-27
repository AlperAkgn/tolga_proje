using System.Security.Claims;
using Afterform.Api.Contracts;
using Afterform.Api.Data;
using Afterform.Api.Models;
using Afterform.Api.Uploads;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace Afterform.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/demos")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DemosController(
    AfterformDbContext database, IDemoFileStorage storage, ILogger<DemosController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DemoHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var artistId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(artistId)) return Unauthorized();

        DemoStatus? selectedStatus = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var name = request.Status.Trim();
            if (!Enum.GetNames<DemoStatus>().Any(value => value.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError(nameof(request.Status),
                    "Choose Submitted, InReview, Accepted or Rejected.");
                return ValidationProblem(ModelState);
            }
            selectedStatus = Enum.Parse<DemoStatus>(name, ignoreCase: true);
        }

        try
        {
            // Filter by session ownership before counting, searching or paging.
            var owned = database.Demos.AsNoTracking().Where(demo => demo.ArtistId == artistId);
            var counts = await owned.GroupBy(demo => demo.Status)
                .Select(group => new { Status = group.Key, Count = group.Count() })
                .ToListAsync(cancellationToken);
            int Count(DemoStatus status) => counts.FirstOrDefault(item => item.Status == status)?.Count ?? 0;
            var summary = new DemoStatusCounts(counts.Sum(item => item.Count),
                Count(DemoStatus.Submitted), Count(DemoStatus.InReview),
                Count(DemoStatus.Accepted), Count(DemoStatus.Rejected));

            var filtered = owned;
            if (selectedStatus is { } status) filtered = filtered.Where(demo => demo.Status == status);
            var search = request.Search?.Trim();
            if (!string.IsNullOrEmpty(search))
                filtered = filtered.Where(demo => demo.Title.Contains(search) || demo.Genre.Contains(search));

            var totalCount = await filtered.CountAsync(cancellationToken);
            var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);
            var page = Math.Clamp(request.Page, 1, Math.Max(1, totalPages));
            var demos = await filtered.OrderByDescending(demo => demo.SubmittedAtUtc)
                .ThenByDescending(demo => demo.Id)
                .Skip((page - 1) * request.PageSize).Take(request.PageSize)
                .ToListAsync(cancellationToken);

            return Ok(new DemoHistoryResponse(artistId, demos.Select(DemoResponse.From).ToArray(),
                page, request.PageSize, totalCount, totalPages, summary));
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            logger.LogError(exception, "The artist's demo history could not be read.");
            return Problem(statusCode: 503, title: "Demo history is temporarily unavailable.");
        }
    }

    [HttpGet("options")]
    public IActionResult Options() => Ok(new
    {
        maxFileSizeBytes = DemoUploadRules.MaxFileBytes,
        acceptedExtensions = DemoUploadRules.Extensions,
        genres = DemoUploadRules.Genres
    });

    [HttpPost]
    [Consumes("multipart/form-data")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(DemoUploadRules.UploadRatePolicy)]
    [RequestSizeLimit(DemoUploadRules.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = DemoUploadRules.MaxFileBytes,
        ValueCountLimit = 8, ValueLengthLimit = 4096, MultipartHeadersCountLimit = 16)]
    public async Task<IActionResult> Upload([FromForm] DemoUploadRequest request,
        CancellationToken cancellationToken)
    {
        var artistId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(artistId)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.TrackTitle))
            ModelState.AddModelError(nameof(request.TrackTitle), "Enter a track title.");
        if (!DemoUploadRules.Genres.Contains(request.Genre.Trim()))
            ModelState.AddModelError(nameof(request.Genre), "Choose one of the supported genres.");
        if (!request.RightsConfirmation)
            ModelState.AddModelError(nameof(request.RightsConfirmation), "Confirm your right to share this recording.");
        if (Request.Form.Files.Count != 1 || request.AudioFile is null)
            ModelState.AddModelError(nameof(request.AudioFile), "Choose exactly one audio file.");
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        StoredDemoFile? saved = null;
        var recorded = false;
        try
        {
            saved = await storage.SaveAsync(request.AudioFile!, cancellationToken);
            var now = DateTime.UtcNow;
            var demo = new DemoSubmission
            {
                // Ownership always comes from the authenticated session.
                ArtistId = artistId,
                Title = request.TrackTitle.Trim(),
                Genre = request.Genre.Trim(),
                Description = string.IsNullOrWhiteSpace(request.ArtistNote) ? null : request.ArtistNote.Trim(),
                Status = DemoStatus.Submitted,
                SubmittedAtUtc = now,
                RightsConfirmedAtUtc = now,
                StorageKey = saved.StorageKey,
                OriginalFileName = saved.OriginalFileName,
                ContentType = saved.ContentType,
                FileSizeBytes = saved.SizeBytes,
                FileSha256 = saved.Sha256
            };
            database.Demos.Add(demo);
            await database.SaveChangesAsync(cancellationToken);
            recorded = true;
            return CreatedAtAction(nameof(Get), new { id = demo.Id }, DemoResponse.From(demo));
        }
        catch (DemoFileValidationException exception)
        {
            ModelState.AddModelError(nameof(request.AudioFile), exception.Message);
            return ValidationProblem(ModelState);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            logger.LogError(exception, "The demo database record could not be saved.");
            return Problem(statusCode: 503, title: "Demo services are temporarily unavailable.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogError(exception, "Private demo storage is unavailable.");
            return Problem(statusCode: 503, title: "Audio storage is temporarily unavailable.");
        }
        finally
        {
            if (saved is not null && !recorded)
            {
                // A lost database response can leave the commit outcome unknown.
                // Delete only after confirming no record refers to this file.
                try
                {
                    using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var exists = await database.Demos.AsNoTracking().AnyAsync(
                        demo => demo.StorageKey == saved.StorageKey, cleanupTimeout.Token);
                    if (!exists) storage.Delete(saved.StorageKey);
                }
                catch (Exception exception)
                {
                    // Keep the file for reconciliation rather than risk deleting
                    // audio whose database write actually succeeded.
                    logger.LogWarning(exception, "An interrupted demo upload needs storage reconciliation.");
                }
            }
        }
    }

    [HttpGet("{id:long:min(1)}")]
    public async Task<IActionResult> Get(long id, CancellationToken cancellationToken)
    {
        try
        {
            var demo = await OwnDemo(id, cancellationToken);
            return demo is null ? NotFound() : Ok(DemoResponse.From(demo));
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            logger.LogError(exception, "The demo record could not be read.");
            return Problem(statusCode: 503, title: "Demo services are temporarily unavailable.");
        }
    }

    [HttpGet("{id:long:min(1)}/audio")]
    public async Task<IActionResult> Audio(long id, [FromQuery] bool download,
        CancellationToken cancellationToken)
    {
        try
        {
            var demo = await OwnDemo(id, cancellationToken);
            if (demo is null) return NotFound();
            var stream = storage.OpenRead(demo.StorageKey);
            if (stream is null) return NotFound();
            Response.Headers.CacheControl = "private, no-store";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(stream, demo.ContentType, download ? demo.OriginalFileName : null,
                enableRangeProcessing: true);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            logger.LogError(exception, "Audio ownership could not be checked.");
            return Problem(statusCode: 503, title: "Demo services are temporarily unavailable.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogError(exception, "The private demo file could not be opened.");
            return Problem(statusCode: 503, title: "Audio storage is temporarily unavailable.");
        }
    }

    private Task<DemoSubmission?> OwnDemo(long id, CancellationToken cancellationToken)
    {
        var artistId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        // Missing IDs and another artist's IDs produce the same 404 response.
        return database.Demos.AsNoTracking()
            .SingleOrDefaultAsync(demo => demo.Id == id && demo.ArtistId == artistId, cancellationToken);
    }

    private static bool IsDatabaseFailure(Exception exception) =>
        exception is MySqlException or DbUpdateException or System.Security.Authentication.AuthenticationException
        || exception.InnerException is MySqlException;
}
