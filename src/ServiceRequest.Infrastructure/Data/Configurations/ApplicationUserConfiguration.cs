using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceRequest.Domain.Entities;

namespace ServiceRequest.Infrastructure.Data.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Username)
            .HasMaxLength(100)
            .IsRequired()
            .UseCollation("NOCASE");

        builder.HasIndex(user => user.Username)
            .IsUnique();

        builder.Property(user => user.DisplayName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(user => user.Email)
            .HasMaxLength(320)
            .IsRequired()
            .UseCollation("NOCASE");

        builder.HasIndex(user => user.Email)
            .IsUnique();

        builder.Property(user => user.Role)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(user => user.PasswordHash)
            .HasMaxLength(500)
            .IsRequired()
            .HasDefaultValue(string.Empty);
    }
}
