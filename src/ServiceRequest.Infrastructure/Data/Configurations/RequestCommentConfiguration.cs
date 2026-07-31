using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceRequest.Domain.Entities;

namespace ServiceRequest.Infrastructure.Data.Configurations;

public sealed class RequestCommentConfiguration : IEntityTypeConfiguration<RequestComment>
{
    public void Configure(EntityTypeBuilder<RequestComment> builder)
    {
        builder.ToTable("RequestComments");

        builder.HasKey(comment => comment.Id);

        builder.Property(comment => comment.Content)
            .HasMaxLength(4000)
            .IsRequired();

        // See SupportRequestConfiguration.CreatedAt: SQLite's EF Core provider cannot translate
        // ORDER BY on DateTimeOffset columns. CreatedAt is always UTC, so this round-trips losslessly.
        builder.Property(comment => comment.CreatedAt)
            .HasConversion(
                toProvider => toProvider.UtcDateTime,
                fromProvider => new DateTimeOffset(fromProvider, TimeSpan.Zero));

        builder.HasOne(comment => comment.SupportRequest)
            .WithMany()
            .HasForeignKey(comment => comment.ServiceRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(comment => comment.AuthorUser)
            .WithMany()
            .HasForeignKey(comment => comment.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(comment => comment.ServiceRequestId);
        builder.HasIndex(comment => comment.AuthorUserId);
        builder.HasIndex(comment => comment.CreatedAt);
    }
}
