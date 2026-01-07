using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Data;

/// Фабрика для создания DataContext во время выполнения миграций (dotnet ef).
/// НЕ используется при обычном запуске приложения — там контекст создаётся в Program.cs.
public sealed class DataContextDesignFactory : IDesignTimeDbContextFactory<DataContext>
{
    public DataContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();

        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile(Path.Combine("..", "Console", "appsettings.json"), optional: true)
            .Build();

        var connectionString =
            config.GetConnectionString("DefaultConnection") ??
            Environment.GetEnvironmentVariable("DefaultConnection");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' не найден. " +
                "Убедитесь, что appsettings.json существует или задана переменная окружения DefaultConnection.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<DataContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new DataContext(optionsBuilder.Options);
    }
}
