using Microsoft.EntityFrameworkCore;
using ServiceRequest.Domain.Entities;

namespace ServiceRequest.Infrastructure.Data;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();

    public DbSet<RequestCategory> RequestCategories => Set<RequestCategory>();

    public DbSet<SupportRequest> SupportRequests => Set<SupportRequest>();

    public DbSet<RequestComment> RequestComments => Set<RequestComment>();

    public DbSet<RequestHistory> RequestHistory => Set<RequestHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
