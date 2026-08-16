using GillyTracker.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GillyTracker.Core;

/// <summary>
/// Background service that applies EF Core migrations on application startup.
/// This ensures the database schema is up-to-date before the application begins serving requests.
/// </summary>
internal sealed class DatabaseMigrationService(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime lifetime,
    ILogger<DatabaseMigrationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for the application to be fully started
        await Task.Yield();

        try
        {
            logger.LogInformation("Applying database migrations...");

            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await RelocateMigrationsHistoryTableAsync(dbContext, stoppingToken);
            await dbContext.Database.MigrateAsync(stoppingToken);

            logger.LogInformation("Database migrations applied successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while applying database migrations");
            
            // Stop the application if migrations fail
            lifetime.StopApplication();
            throw;
        }
    }

    /// <summary>
    /// EF Core does not automatically move the "__EFMigrationsHistory" table when its configured schema changes
    /// (see https://learn.microsoft.com/ef/core/managing-schemas/migrations/history-table). Without this, a
    /// database that already has migrations recorded under the old "dbo" schema would appear - once the app is
    /// configured to look for history in the "GillyTracker" schema - to have no migrations applied at all, causing
    /// EF to attempt to re-run every migration from scratch and fail against the already-existing tables.
    /// This physically relocates the table (preserving its rows) the first time it runs against such a database.
    /// It is a no-op for brand-new databases and for databases that have already been relocated.
    /// </summary>
    private static async Task RelocateMigrationsHistoryTableAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        if (dbContext.Database.ProviderName != "Microsoft.EntityFrameworkCore.SqlServer")
        {
            return;
        }

        if (!await dbContext.Database.CanConnectAsync(cancellationToken))
        {
            // Database doesn't exist yet; EF will create it (and the history table directly in the
            // GillyTracker schema) as part of applying migrations.
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            IF SCHEMA_ID(N'GillyTracker') IS NULL EXEC(N'CREATE SCHEMA [GillyTracker]');
            IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NOT NULL
            BEGIN
                -- Drop a stray, empty history table that a prior failed migration attempt may have
                -- already created in the GillyTracker schema before this fix existed.
                IF OBJECT_ID(N'[GillyTracker].[__EFMigrationsHistory]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM [GillyTracker].[__EFMigrationsHistory])
                    EXEC(N'DROP TABLE [GillyTracker].[__EFMigrationsHistory]');

                IF OBJECT_ID(N'[GillyTracker].[__EFMigrationsHistory]', N'U') IS NULL
                    EXEC(N'ALTER SCHEMA [GillyTracker] TRANSFER [dbo].[__EFMigrationsHistory]');
            END
            """,
            cancellationToken);
    }
}
