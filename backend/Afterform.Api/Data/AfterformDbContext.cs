using Afterform.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Afterform.Api.Data;

public sealed class AfterformDbContext(DbContextOptions<AfterformDbContext> options)
    : IdentityDbContext<ArtistUser>(options)
{
    public DbSet<DemoSubmission> Demos => Set<DemoSubmission>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Required so Identity can create its account and role relationships.
        base.OnModelCreating(builder);

        // Explicit lengths keep the UTF-8 indexes within MySQL's index size limits.
        const int keyLength = 128;
        builder.Entity<ArtistUser>().Property(x => x.Id).HasMaxLength(keyLength);
        builder.Entity<IdentityRole>().Property(x => x.Id).HasMaxLength(keyLength);
        builder.Entity<IdentityUserClaim<string>>().Property(x => x.UserId).HasMaxLength(keyLength);
        builder.Entity<IdentityRoleClaim<string>>().Property(x => x.RoleId).HasMaxLength(keyLength);
        builder.Entity<IdentityUserRole<string>>().Property(x => x.UserId).HasMaxLength(keyLength);
        builder.Entity<IdentityUserRole<string>>().Property(x => x.RoleId).HasMaxLength(keyLength);
        builder.Entity<IdentityUserLogin<string>>().Property(x => x.UserId).HasMaxLength(keyLength);
        builder.Entity<IdentityUserLogin<string>>().Property(x => x.LoginProvider).HasMaxLength(keyLength);
        builder.Entity<IdentityUserLogin<string>>().Property(x => x.ProviderKey).HasMaxLength(keyLength);
        builder.Entity<IdentityUserToken<string>>().Property(x => x.UserId).HasMaxLength(keyLength);
        builder.Entity<IdentityUserToken<string>>().Property(x => x.LoginProvider).HasMaxLength(keyLength);
        builder.Entity<IdentityUserToken<string>>().Property(x => x.Name).HasMaxLength(keyLength);

        builder.Entity<ArtistUser>().Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
        builder.Entity<ArtistUser>().Property(x => x.CreatedAtUtc).HasColumnType("datetime(6)");

        builder.ApplyConfigurationsFromAssembly(typeof(AfterformDbContext).Assembly);
    }
}

