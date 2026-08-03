using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ServiceRequest.Domain.Entities;
using ServiceRequest.Domain.Enums;
using ServiceRequest.Infrastructure.Data;

namespace ServiceRequest.Infrastructure.Seed;

public static class DevelopmentUserSeeder
{
    private static readonly (string Username, string DisplayName, string Email, UserRole Role, string Password)[] SeedUsers =
    [
        ("admin",     "Development Admin",           "admin@example.test",     UserRole.Admin,        "Admin123!"),
        ("agent",     "Development Support Agent",   "agent@example.test",     UserRole.SupportAgent, "Agent123!"),
        ("agent2",    "Development Support Agent 2", "agent2@example.test",    UserRole.SupportAgent, "Agent2123!"),
        ("employee",  "Development Employee",        "employee@example.test",  UserRole.Employee,     "Employee123!"),
        ("employee2", "Development Employee 2",      "employee2@example.test", UserRole.Employee,     "Employee2123!"),
    ];

    public static async Task SeedAsync(
        ApplicationDbContext dbContext,
        IPasswordHasher<ApplicationUser> passwordHasher,
        CancellationToken cancellationToken = default)
    {
        foreach (var seedUser in SeedUsers)
        {
            var exists = await dbContext.Users
                .AnyAsync(user => user.Username == seedUser.Username, cancellationToken);

            if (exists)
            {
                continue;
            }

            var user = new ApplicationUser(seedUser.Username, seedUser.DisplayName, seedUser.Email, seedUser.Role);
            var hash = passwordHasher.HashPassword(user, seedUser.Password);
            user.SetPasswordHash(hash);

            dbContext.Users.Add(user);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
