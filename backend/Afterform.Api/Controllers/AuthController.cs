using Afterform.Api.Contracts;
using Afterform.Api.Models;
using Afterform.Api.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace Afterform.Api.Controllers;

[ApiController]
[Route("api/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController(
    UserManager<ArtistUser> users,
    SignInManager<ArtistUser> signIn,
    IAntiforgery antiforgery,
    LoginTimingGuard timing,
    ILogger<AuthController> logger) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("csrf")]
    public IActionResult Csrf() =>
        Ok(new { requestToken = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });

    [AllowAnonymous]
    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(ArtistIdentityConfiguration.LoginRatePolicy)]
    [RequestSizeLimit(4096)]
    public async Task<IActionResult> Login(ArtistLoginRequest request)
    {
        try
        {
            var artist = await users.FindByNameAsync(request.UserName.Trim());
            if (artist is null)
            {
                timing.VerifyDummy(request.Password);
                return InvalidCredentials();
            }

            var result = await signIn.PasswordSignInAsync(
                artist, request.Password, isPersistent: false, lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                if (result.IsLockedOut || result.IsNotAllowed)
                    timing.VerifyDummy(request.Password);
                return InvalidCredentials();
            }
            return Ok(ArtistSessionResponse.From(artist));
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            logger.LogError(exception, "Artist sign-in database operation failed.");
            return DatabaseUnavailable();
        }
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        try
        {
            var artist = await users.GetUserAsync(User);
            if (artist is null)
            {
                await signIn.SignOutAsync();
                return Unauthorized();
            }
            return Ok(ArtistSessionResponse.From(artist));
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            logger.LogError(exception, "Artist session database operation failed.");
            return DatabaseUnavailable();
        }
    }

    [Authorize]
    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return NoContent();
    }

    private ObjectResult InvalidCredentials() => Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Sign-in unsuccessful.",
        detail: "Check your username and password. If you have tried several times, wait 15 minutes or contact the label.");

    private ObjectResult DatabaseUnavailable() => Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "Artist account service is temporarily unavailable.");

    private static bool IsDatabaseFailure(Exception exception) =>
        exception is MySqlException or DbUpdateException or System.Security.Authentication.AuthenticationException
        || exception.InnerException is MySqlException;
}
