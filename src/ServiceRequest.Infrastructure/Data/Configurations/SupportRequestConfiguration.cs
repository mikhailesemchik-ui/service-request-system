using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceRequest.Domain.Entities;

namespace ServiceRequest.Infrastructure.Data.Configurations;

public sealed class SupportRequestConfiguration : IEntityTypeConfiguration<SupportRequest>
{
    public void Configure(EntityTypeBuilder<SupportRequest> builder)
    {
        builder.ToTable("ServiceRequests");

        builder.HasKey(request => request.Id);

        builder.Property(request => request.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(request => request.Description)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(request => request.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(request => request.Priority)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        // SQLite's EF Core provider refuses to translate ORDER BY on DateTimeOffset columns
        // (it cannot guarantee correct ordering across mixed offsets). CreatedAt is always UTC
        // (see SupportRequest constructor), so storing it as DateTime round-trips losslessly
        // and lets the required "newest first" ordering run in the database.
        builder.Property(request => request.CreatedAt)
            .HasConversion(
                toProvider => toProvider.UtcDateTime,
                fromProvider => new DateTimeOffset(fromProvider, TimeSpan.Zero));

        builder.HasOne(request => request.Category)
            .WithMany()
            .HasForeignKey(request => request.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(request => request.CreatedByUser)
            .WithMany()
            .HasForeignKey(request => request.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(request => request.AssignedToUser)
            .WithMany()
            .HasForeignKey(request => request.AssignedToUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(request => request.Status);
        builder.HasIndex(request => request.Priority);
        builder.HasIndex(request => request.CategoryId);
        builder.HasIndex(request => request.CreatedByUserId);
        builder.HasIndex(request => request.AssignedToUserId);
        builder.HasIndex(request => request.CreatedAt);
    }
}
