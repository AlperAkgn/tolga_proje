using Microsoft.AspNetCore.Identity;

namespace Afterform.Api.Models;

// Identity supplies UserName, PasswordHash and the other account fields.
// Account creation and authentication are implemented in plan step 6.
public sealed class ArtistUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<DemoSubmission> Demos { get; set; } = new List<DemoSubmission>();
}

