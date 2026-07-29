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
