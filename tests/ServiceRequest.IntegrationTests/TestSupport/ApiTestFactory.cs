using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceRequest.Application.Authentication;
using ServiceRequest.Domain.Entities;
using ServiceRequest.Domain.Enums;
using ServiceRequest.Infrastructure.Data;

namespace ServiceRequest.IntegrationTests.TestSupport;

public sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    public const string TestSigningKey = "integration-test-signing-key-not-for-production-use-only-123456";
    public const string TestIssuer = "ServiceRequest.Api.IntegrationTests";
    public const string TestAudience = "ServiceRequest.Client.IntegrationTests";

    public const string AdminUsername = "test-admin";
    public const string AdminPassword = "TestAdmin123!";
    public const string SupportAgentUsername = "test-agent";
    public const string SupportAgentPassword = "TestAgent123!";
    public const string EmployeeUsername = "test-employee";
    public const string EmployeePassword = "TestEmployee123!";
    public const string InactiveUsername = "test-inactive";
    public const string InactivePassword = "TestInactive123!";

    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"service-requests-api-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTesting");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                service => service.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite($"Data Source={_databasePath};Pooling=False"));

            services.PostConfigure<JwtOptions>(options =>
            {
                options.Issuer = TestIssuer;
                options.Audience = TestAudience;
                options.SigningKey = TestSigningKey;
                options.ExpirationMinutes = 60;
            });

            services.AddControllers().AddApplicationPart(typeof(PolicyProbeController).Assembly);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using (var scope = host.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Database.Migrate();

            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<ApplicationUser>>();
            SeedTestUser(dbContext, passwordHasher, AdminUsername, "Test Admin", "test-admin@example.test", UserRole.Admin, AdminPassword, isActive: true);
            SeedTestUser(dbContext, passwordHasher, SupportAgentUsername, "Test Support Agent", "test-agent@example.test", UserRole.SupportAgent, SupportAgentPassword, isActive: true);
            SeedTestUser(dbContext, passwordHasher, EmployeeUsername, "Test Employee", "test-employee@example.test", UserRole.Employee, EmployeePassword, isActive: true);
            SeedTestUser(dbContext, passwordHasher, InactiveUsername, "Test Inactive User", "test-inactive@example.test", UserRole.Employee, InactivePassword, isActive: false);
            dbContext.SaveChanges();
        }

        return host;
    }

    private static void SeedTestUser(
        ApplicationDbContext dbContext,
        IPasswordHasher<ApplicationUser> passwordHasher,
        string username,
        string displayName,
        string email,
        UserRole role,
        string password,
        bool isActive)
    {
        var user = new ApplicationUser(username, displayName, email, role);
        user.SetPasswordHash(passwordHasher.HashPassword(user, password));

        if (!isActive)
        {
            user.SetActiveState(false);
        }

        dbContext.Users.Add(user);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        SqliteConnection.ClearAllPools();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (File.Exists(_databasePath))
                {
                    File.Delete(_databasePath);
                }

                return;
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
        }
    }
}
