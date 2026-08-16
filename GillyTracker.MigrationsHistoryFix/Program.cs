using Microsoft.Data.SqlClient;

// This tool exists to work around a limitation of EF Core Migrations: when the schema of the
// "__EFMigrationsHistory" table changes (see GillyTracker.Data's ApplicationDbContext /
// GillyTracker.Core's DependencyInjection), EF Core does not physically relocate the existing
// table for databases that already have migrations recorded under its old location. It instead
// determines which migrations are "pending" using the *newly configured* location, which - if the
// table hasn't actually been moved yet - looks empty, causing EF to try to re-apply every
// migration from scratch and fail against tables that already exist.
//
// Run this once, before applying migrations (via `dotnet ef database update`, an EF migrations
// bundle, or Database.MigrateAsync()), to physically move "__EFMigrationsHistory" from "dbo" into
// the "GillyTracker" schema, preserving all of its previously recorded rows. It is a no-op for
// brand-new databases (nothing to move yet) and for databases that have already been migrated.
//
// See GillyTracker.Core.DatabaseMigrationService.RelocateMigrationsHistoryTableAsync, which
// performs the same fix-up automatically before startup migrations run in-process; this
// standalone tool covers deployment paths (like the CI/CD migrations bundle) that apply
// migrations outside of the running application.

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("The 'ConnectionStrings__Database' environment variable must be set.");
    return 1;
}

const string relocateHistoryTableSql = """
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
    """;

try
{
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    await using var command = connection.CreateCommand();
    command.CommandText = relocateHistoryTableSql;
    await command.ExecuteNonQueryAsync();

    Console.WriteLine("Migrations history table schema check complete.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to relocate the migrations history table: {ex}");
    return 1;
}
