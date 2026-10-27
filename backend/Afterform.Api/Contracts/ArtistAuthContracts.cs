using System.ComponentModel.DataAnnotations;
using Afterform.Api.Models;

namespace Afterform.Api.Contracts;

public sealed class ArtistLoginRequest
{
    [Required, StringLength(64)]
    public string UserName { get; init; } = string.Empty;

    [Required, StringLength(128)]
    public string Password { get; init; } = string.Empty;
}

public sealed record ArtistSessionResponse(string Id, string UserName, string DisplayName)
{
    public static ArtistSessionResponse From(ArtistUser artist) =>
        new(artist.Id, artist.UserName ?? string.Empty, artist.DisplayName);
}
