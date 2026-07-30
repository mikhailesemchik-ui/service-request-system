using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceRequest.Domain.Entities;

namespace ServiceRequest.Infrastructure.Data.Configurations;

public sealed class RequestHistoryConfiguration : IEntityTypeConfiguration<RequestHistory>
{
    public void Configure(EntityTypeBuilder<RequestHistory> builder)
    {
        builder.ToTable("RequestHistory");

        builder.HasKey(history => history.Id);

        builder.Property(history => history.Action)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(history => history.PreviousValue)
            .HasMaxLength(1000);

        builder.Property(history => history.NewValue)
            .HasMaxLength(1000);

        // See SupportRequestConfiguration.CreatedAt: SQLite's EF Core provider cannot translate
        // ORDER BY on DateTimeOffset columns. CreatedAt is always UTC, so this round-trips losslessly.
        builder.Property(history => history.CreatedAt)
            .HasConversion(
                toProvider => toProvider.UtcDateTime,
                fromProvider => new DateTimeOffset(fromProvider, TimeSpan.Zero));

        builder.HasOne(history => history.SupportRequest)
            .WithMany()
            .HasForeignKey(history => history.ServiceRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(history => history.ChangedByUser)
            .WithMany()
            .HasForeignKey(history => history.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(history => history.ServiceRequestId);
        builder.HasIndex(history => history.ChangedByUserId);
        builder.HasIndex(history => history.CreatedAt);
    }
}
