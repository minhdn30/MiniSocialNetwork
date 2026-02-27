using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace CloudM.Infrastructure.Data
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
            var currentDirectory = Directory.GetCurrentDirectory();

            var apiProjectPath = Path.Combine(currentDirectory, "..", "CloudM.API");
            if (!Directory.Exists(apiProjectPath))
            {
                apiProjectPath = Path.Combine(currentDirectory, "CloudM.API");
            }

            var configBasePath = Directory.Exists(apiProjectPath) ? apiProjectPath : currentDirectory;

            var configuration = new ConfigurationBuilder()
                .SetBasePath(configBasePath)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = configuration.GetConnectionString("Default")
                ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Connection string 'Default' was not found for design-time AppDbContext creation.");
            }

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.CommandTimeout(30);
            });

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
