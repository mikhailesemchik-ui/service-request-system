using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceRequest.Domain.Entities;

namespace ServiceRequest.Infrastructure.Data.Configurations;

public sealed class RequestCategoryConfiguration : IEntityTypeConfiguration<RequestCategory>
{
    public void Configure(EntityTypeBuilder<RequestCategory> builder)
    {
        builder.ToTable("RequestCategories");

        builder.HasKey(category => category.Id);

        builder.Property(category => category.Name)
            .HasMaxLength(100)
            .IsRequired()
            .UseCollation("NOCASE");

        builder.HasIndex(category => category.Name)
            .IsUnique();

        builder.Property(category => category.Description)
            .HasMaxLength(500);
    }
}
