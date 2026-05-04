using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Simcag.ProcessingService.Infrastructure.Persistence;

public class ProcessingDbContextFactory : IDesignTimeDbContextFactory<ProcessingDbContext>
{
    public ProcessingDbContext CreateDbContext(string[] args)
    {
        DotNetEnv.Env.NoClobber().Load();

        var host = Environment.GetEnvironmentVariable("DB__HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("DB__PORT") ?? "5432";
        var database = Environment.GetEnvironmentVariable("DB__NAME") ?? "processingdb";
        var user = Environment.GetEnvironmentVariable("DB__USER") ?? "postgres";
        var password = Environment.GetEnvironmentVariable("DB__PASSWORD") ?? "postgres";

        var connectionString = $"Host={host};Port={port};Database={database};Username={user};Password={password}";

        var optionsBuilder = new DbContextOptionsBuilder<ProcessingDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ProcessingDbContext(optionsBuilder.Options);
    }
}
