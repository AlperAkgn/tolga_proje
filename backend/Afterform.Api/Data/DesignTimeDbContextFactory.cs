using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MySql.Data.MySqlClient;

namespace Afterform.Api.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AfterformDbContext>
{
    public AfterformDbContext CreateDbContext(string[] args)
    {
        var projectPath = Directory.GetCurrentDirectory();
        if (!File.Exists(Path.Combine(projectPath, "Afterform.Api.csproj")))
        {
            projectPath = Path.GetFullPath(Path.Combine(projectPath, "backend/Afterform.Api"));
        }

        var localSettingsPath = Path.GetFullPath(Path.Combine(
            projectPath, "../../.local/backend.settings.json"));

        var configuration = new ConfigurationBuilder()
            .SetBasePath(projectPath)
            .AddJsonFile("appsettings.json")
            .AddJsonFile(localSettingsPath, optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        var connectionString = configuration.GetConnectionString("AfterformMigrations");
        // Require real configuration even when only generating a migration.
        // Never silently attempt a database update with placeholder credentials.
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Configure ConnectionStrings:AfterformMigrations in {localSettingsPath} "
                + "or environment variables before using EF commands.");
        }

        var databaseSettings = new MySqlConnectionStringBuilder(connectionString);
        Console.WriteLine($"EF configuration: database={databaseSettings.Database}; "
            + $"user={databaseSettings.UserID}; server={databaseSettings.Server}:{databaseSettings.Port}.");

        return new AfterformDbContext(
            new DbContextOptionsBuilder<AfterformDbContext>()
                .UseMySQL(connectionString)
                .Options);
    }
}
