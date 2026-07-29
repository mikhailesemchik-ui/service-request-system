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
    }
}
