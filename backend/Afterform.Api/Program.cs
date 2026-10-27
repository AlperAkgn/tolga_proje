using Afterform.Api.Data;
using Afterform.Api.Hosting;
using Afterform.Api.Security;
using Afterform.Api.Uploads;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

// Docker can check readiness without installing curl in the runtime image.
if (args is ["--health-check"])
{
    Environment.ExitCode = await ContainerReadiness.RunAsync();
    return;
}

var provisionArtist = args.Contains("--provision-artist", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(args
    .Where(argument => argument != "--provision-artist").ToArray());
PhysicalFileProvider? frontendFiles = null;

// Local development credentials live outside the publicly served outputs folder.
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile(
        Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath,
            "../../.local/backend.settings.json")),
        optional: true,
        reloadOnChange: false);
    var frontendPath = Path.GetFullPath(Path.Combine(
        builder.Environment.ContentRootPath, "../../outputs"));
    if (Directory.Exists(frontendPath))
    {
        frontendFiles = new PhysicalFileProvider(frontendPath);
    }
}

// Docker mounts only the runtime database credential as a read-only secret.
// An explicitly configured but missing file is a startup error.
var settingsFile = builder.Configuration["Afterform:SettingsFile"];
if (!string.IsNullOrWhiteSpace(settingsFile))
{
    builder.Configuration.AddJsonFile(Path.GetFullPath(settingsFile),
        optional: false, reloadOnChange: false);
}
// Environment variables and CLI arguments override either settings file.
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args
    .Where(argument => argument != "--provision-artist").ToArray());

var connectionString = builder.Configuration.GetConnectionString("Afterform");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Configure ConnectionStrings:Afterform. For local setup, run scripts/Initialize-Database.ps1.");
}

builder.Services.AddDbContext<AfterformDbContext>(options =>
    options.UseMySQL(connectionString));
builder.AddArtistIdentity();
builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = DemoUploadRules.MaxRequestBytes);
builder.Services.Configure<IISServerOptions>(options =>
    options.MaxRequestBodySize = DemoUploadRules.MaxRequestBytes);
builder.Services.AddSingleton<IDemoFileStorage, LocalDemoFileStorage>();
// ValidateAntiForgeryToken uses MVC ViewFeatures filter services, which
// AddControllers alone does not register. The endpoints still return JSON.
builder.Services.AddControllersWithViews();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
});

var app = builder.Build();

if (provisionArtist)
{
    Environment.ExitCode = await ArtistProvisioner.RunAsync(app.Services);
    await app.DisposeAsync();
    frontendFiles?.Dispose();
    return;
}

// Validate and prepare private storage before accepting any HTTP requests.
_ = app.Services.GetRequiredService<IDemoFileStorage>();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseArtistPageAccess();

if (frontendFiles is not null)
{
    app.Lifetime.ApplicationStopped.Register(frontendFiles.Dispose);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = frontendFiles });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = frontendFiles });
}
else
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}
app.MapControllers();

// Schema changes are applied explicitly with EF migrations, never on every request.
app.Run();
