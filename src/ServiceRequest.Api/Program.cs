using Microsoft.AspNetCore.Identity;
using ServiceRequest.Api.Authentication;
using ServiceRequest.Api.ExceptionHandling;
using ServiceRequest.Api.Extensions;
using ServiceRequest.Application;
using ServiceRequest.Domain.Entities;
using ServiceRequest.Infrastructure;
using ServiceRequest.Infrastructure.Data;
using ServiceRequest.Infrastructure.Seed;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(SwaggerConfiguration.Configure);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiAuthentication();
builder.Services.AddCors(options => CorsConfiguration.Configure(options, builder.Configuration));

var app = builder.Build();

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var seedScope = app.Services.CreateScope();
    var dbContext = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var passwordHasher = seedScope.ServiceProvider.GetRequiredService<IPasswordHasher<ApplicationUser>>();
    await DevelopmentUserSeeder.SeedAsync(dbContext, passwordHasher);
}

app.UseHttpsRedirection();

app.UseCors(CorsConfiguration.PolicyName);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program
{
}
