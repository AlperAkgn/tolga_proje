using System.Text.Json;
using Afterform.Api.Data;
using Afterform.Api.Models;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

var settingsPath = args.Single();
using var settings = JsonDocument.Parse(File.ReadAllText(settingsPath));
var connectionString = settings.RootElement.GetProperty("ConnectionStrings")
    .GetProperty("Afterform").GetString()!;
await using var db = new AfterformDbContext(
    new DbContextOptionsBuilder<AfterformDbContext>().UseMySQL(connectionString).Options);

var initialArtists = await db.Users.CountAsync();
var initialDemos = await db.Demos.CountAsync();
await using (var transaction = await db.Database.BeginTransactionAsync())
{
    var artist = new ArtistUser
    {
        UserName = "schema-check-" + Guid.NewGuid().ToString("N"),
        DisplayName = "Deneme Sanatçısı",
        CreatedAtUtc = DateTime.UtcNow
    };
    db.Users.Add(artist);
    await db.SaveChangesAsync();

    var demo = new DemoSubmission
    {
        ArtistId = artist.Id,
        Title = "İlk Örnek",
        Genre = "Electronic",
        StorageKey = "schema-check/temporary.mp3",
        OriginalFileName = "temporary.mp3",
        ContentType = "audio/mpeg",
        FileSizeBytes = 123
    };
    db.Demos.Add(demo);
    await db.SaveChangesAsync();

    var loaded = await db.Demos.AsNoTracking().Include(x => x.Artist)
        .SingleAsync(x => x.Id == demo.Id);
    if (loaded.Artist.DisplayName != artist.DisplayName ||
        loaded.Title != demo.Title || loaded.Status != DemoStatus.Submitted)
    {
        throw new InvalidOperationException("EF round-trip, UTF-8 or default status check failed.");
    }
    Console.WriteLine("PASS: typed EF insert/read, artist relationship, UTF-8 and Submitted default.");

    var missingArtist = Guid.NewGuid().ToString();
    try
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Demos (ArtistId, Title, Genre, SubmittedAtUtc, StorageKey, OriginalFileName, ContentType, FileSizeBytes) VALUES ({missingArtist}, {"FK check"}, {"Electronic"}, {DateTime.UtcNow}, {"schema-check/missing.mp3"}, {"missing.mp3"}, {"audio/mpeg"}, {123L})");
        throw new InvalidOperationException("MySQL accepted a demo without an existing artist.");
    }
    catch (MySqlException exception) when (exception.Number == 1452)
    {
        Console.WriteLine("PASS: MySQL rejects a demo whose artist does not exist.");
    }

    try
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM AspNetUsers WHERE Id = {artist.Id}");
        throw new InvalidOperationException("MySQL deleted an artist who still owns a demo.");
    }
    catch (MySqlException exception) when (exception.Number == 1451)
    {
        Console.WriteLine("PASS: MySQL restricts deletion of an artist with demos.");
    }
    await transaction.RollbackAsync();
}
if (await db.Users.CountAsync() != initialArtists || await db.Demos.CountAsync() != initialDemos)
{
    throw new InvalidOperationException("The validation transaction was not fully rolled back.");
}
Console.WriteLine("PASS: transaction rolled back; no validation artists or demos remain.");

