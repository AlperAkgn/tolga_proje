using Afterform.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Afterform.Api.Data;

public sealed class DemoSubmissionConfiguration : IEntityTypeConfiguration<DemoSubmission>
{
    public void Configure(EntityTypeBuilder<DemoSubmission> builder)
    {
        builder.ToTable("Demos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ArtistId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Genre).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(24)
            .HasDefaultValue(DemoStatus.Submitted)
            .IsRequired();
        builder.Property(x => x.SubmittedAtUtc).HasColumnType("datetime(6)");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime(6)");
        builder.Property(x => x.StorageKey).HasMaxLength(512).IsRequired();
        builder.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.FileSha256).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RightsConfirmedAtUtc).HasColumnType("datetime(6)");

        builder.HasOne(x => x.Artist)
            .WithMany(x => x.Demos)
            .HasForeignKey(x => x.ArtistId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ArtistId, x.SubmittedAtUtc });
    }
}
