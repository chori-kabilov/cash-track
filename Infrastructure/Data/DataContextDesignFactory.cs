using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Data;

/// <summary>
/// Design-time factory ONLY for migrations. Runtime creation uses Program.cs.
/// Reads connection string from appsettings.json (Console project) or environment variable DefaultConnection.
/// </summary>
public sealed class DataContextDesignFactory : IDesignTimeDbContextFactory<DataContext>
{
    public DataContext CreateDbContext(string[] args)
    {
        // Try to locate appsettings.json in Console project (startup) or current dir
        var basePath = Directory.GetCurrentDirectory();

        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile(Path.Combine("..", "Console", "appsettings.json"), optional: true)
            .Build();

        var connectionString =
            config.GetConnectionString("DefaultConnection") ??
            config["DefaultConnection"] ??
            Environment.GetEnvironmentVariable("DefaultConnection") ??
            "Host=localhost;Database=cashtrack_db;Username=postgres;Password=12345";

        var optionsBuilder = new DbContextOptionsBuilder<DataContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new DataContext(optionsBuilder.Options);
    }
}
