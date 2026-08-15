using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GillyTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveIdentityTablesToGillyTrackerSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Move the EF Core migrations history table into the GillyTracker schema as well, so that all
            // application-owned tables (Identity + EF bookkeeping) live outside of dbo.
            // NOTE: EF Core will not do this automatically for databases where migrations have already been
            // applied against the "dbo" schema (see https://learn.microsoft.com/ef/core/managing-schemas/migrations/history-table).
            // If this migration is being applied via `dotnet ef database update`/`Database.MigrateAsync()` against
            // such a database *after* the MigrationsHistoryTable schema has been changed in code, this statement will
            // never execute because EF Core will already believe no migrations have been applied. In that case, run
            // the following statement manually against the target database BEFORE deploying/running this migration:
            //   ALTER SCHEMA [GillyTracker] TRANSFER [dbo].[__EFMigrationsHistory];
            migrationBuilder.Sql("""
                IF SCHEMA_ID(N'GillyTracker') IS NULL EXEC(N'CREATE SCHEMA [GillyTracker]');
                IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NOT NULL
                    AND OBJECT_ID(N'[GillyTracker].[__EFMigrationsHistory]', N'U') IS NULL
                    EXEC(N'ALTER SCHEMA [GillyTracker] TRANSFER [dbo].[__EFMigrationsHistory]');
                """);

            migrationBuilder.RenameTable(
                name: "AspNetUserTokens",
                newName: "AspNetUserTokens",
                newSchema: "GillyTracker");

            migrationBuilder.RenameTable(
                name: "AspNetUsers",
                newName: "AspNetUsers",
                newSchema: "GillyTracker");

            migrationBuilder.RenameTable(
                name: "AspNetUserRoles",
                newName: "AspNetUserRoles",
                newSchema: "GillyTracker");

            migrationBuilder.RenameTable(
                name: "AspNetUserPasskeys",
                newName: "AspNetUserPasskeys",
                newSchema: "GillyTracker");

            migrationBuilder.RenameTable(
                name: "AspNetUserLogins",
                newName: "AspNetUserLogins",
                newSchema: "GillyTracker");

            migrationBuilder.RenameTable(
                name: "AspNetUserClaims",
                newName: "AspNetUserClaims",
                newSchema: "GillyTracker");

            migrationBuilder.RenameTable(
                name: "AspNetRoles",
                newName: "AspNetRoles",
                newSchema: "GillyTracker");

            migrationBuilder.RenameTable(
                name: "AspNetRoleClaims",
                newName: "AspNetRoleClaims",
                newSchema: "GillyTracker");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "AspNetUserTokens",
                schema: "GillyTracker",
                newName: "AspNetUserTokens");

            migrationBuilder.RenameTable(
                name: "AspNetUsers",
                schema: "GillyTracker",
                newName: "AspNetUsers");

            migrationBuilder.RenameTable(
                name: "AspNetUserRoles",
                schema: "GillyTracker",
                newName: "AspNetUserRoles");

            migrationBuilder.RenameTable(
                name: "AspNetUserPasskeys",
                schema: "GillyTracker",
                newName: "AspNetUserPasskeys");

            migrationBuilder.RenameTable(
                name: "AspNetUserLogins",
                schema: "GillyTracker",
                newName: "AspNetUserLogins");

            migrationBuilder.RenameTable(
                name: "AspNetUserClaims",
                schema: "GillyTracker",
                newName: "AspNetUserClaims");

            migrationBuilder.RenameTable(
                name: "AspNetRoles",
                schema: "GillyTracker",
                newName: "AspNetRoles");

            migrationBuilder.RenameTable(
                name: "AspNetRoleClaims",
                schema: "GillyTracker",
                newName: "AspNetRoleClaims");

            // See the note in Up() regarding the caveats of moving the history table for pre-existing databases.
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[GillyTracker].[__EFMigrationsHistory]', N'U') IS NOT NULL
                    AND OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NULL
                    EXEC(N'ALTER SCHEMA [dbo] TRANSFER [GillyTracker].[__EFMigrationsHistory]');
                """);
        }
    }
}
