using System.Security.Cryptography;
using Afterform.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Afterform.Api.Security;

// Spend a password verification on unknown/locked accounts too, avoiding an
// immediate response that would make username enumeration much easier.
public sealed class LoginTimingGuard
{
    private readonly PasswordHasher<ArtistUser> hasher;
    private readonly ArtistUser dummyUser = new();
    private readonly string dummyHash;

    public LoginTimingGuard(IOptions<PasswordHasherOptions> options)
    {
        hasher = new PasswordHasher<ArtistUser>(options);
        dummyHash = hasher.HashPassword(dummyUser,
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
    }

    public void VerifyDummy(string password) =>
        hasher.VerifyHashedPassword(dummyUser, dummyHash, password);
}
