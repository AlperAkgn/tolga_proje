using Afterform.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Afterform.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(
    AfterformDbContext database,
    IWebHostEnvironment environment,
    ILogger<HealthController> logger) : ControllerBase
{
    [HttpGet("live")]
    public IActionResult Live() => Ok(new
    {
        status = "ok",
        service = "Afterform.Api",
        timestampUtc = DateTime.UtcNow
    });

    [HttpGet("ready")]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        try
        {
            if (!await database.Database.CanConnectAsync(cancellationToken))
            {
                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Database is unavailable.");
            }

            var pending = await database.Database.GetPendingMigrationsAsync(cancellationToken);
            if (pending.Any())
            {
                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Database schema needs a migration.");
            }

            return Ok(new { status = "ready", database = "connected", schema = "upToDate" });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The database readiness check failed.");
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Database readiness check failed.");
        }
    }

    // Learning aid for this development phase; this route is absent in production.
    [HttpGet("schema")]
    public async Task<IActionResult> Schema(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        var migrations = await database.Database.GetAppliedMigrationsAsync(cancellationToken);
        var tables = database.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Where(name => name is not null)
            .Distinct()
            .Order()
            .ToArray();

        return Ok(new
        {
            tables,
            appliedMigrations = migrations,
            relationship = "AspNetUsers.Id -> Demos.ArtistId (one artist, many demos)",
            authentication = "ASP.NET Core Identity with a protected cookie session",
            fileUpload = "Private audio uploads with authenticated ownership checks and SHA-256 metadata"
        });
    }
}
