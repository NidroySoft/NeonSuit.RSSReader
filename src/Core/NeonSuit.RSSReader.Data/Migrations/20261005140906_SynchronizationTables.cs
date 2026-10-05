using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeonSuit.RSSReader.Data.Migrations
{
    /// <inheritdoc />
    public partial class SynchronizationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SyncErrors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ErrorTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TaskType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: false),
                    StackTrace = table.Column<string>(type: "TEXT", nullable: true),
                    IsRecoverable = table.Column<bool>(type: "INTEGER", nullable: false),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Resolved = table.Column<bool>(type: "INTEGER", nullable: false),
                    ResolutionNotes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncErrors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CurrentStatus = table.Column<string>(type: "TEXT", nullable: false),
                    IsPaused = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastSyncCompleted = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MaxSyncDurationMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncStatistics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TotalSyncCycles = table.Column<int>(type: "INTEGER", nullable: false),
                    SuccessfulSyncs = table.Column<int>(type: "INTEGER", nullable: false),
                    FailedSyncs = table.Column<int>(type: "INTEGER", nullable: false),
                    AverageSyncDurationSeconds = table.Column<double>(type: "REAL", nullable: false),
                    TotalSyncTimeSeconds = table.Column<double>(type: "REAL", nullable: false),
                    ArticlesProcessed = table.Column<int>(type: "INTEGER", nullable: false),
                    FeedsUpdated = table.Column<int>(type: "INTEGER", nullable: false),
                    TagsApplied = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncStatistics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncTaskConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TaskType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IntervalMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    Priority = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    MaxRetries = table.Column<int>(type: "INTEGER", nullable: false),
                    RetryDelayMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    LastScheduled = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NextScheduled = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncTaskConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncTaskExecutions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TaskType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsManual = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequestId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    ResultsJson = table.Column<string>(type: "TEXT", nullable: true),
                    DurationSeconds = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncTaskExecutions", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncErrors");

            migrationBuilder.DropTable(
                name: "SyncStates");

            migrationBuilder.DropTable(
                name: "SyncStatistics");

            migrationBuilder.DropTable(
                name: "SyncTaskConfigs");

            migrationBuilder.DropTable(
                name: "SyncTaskExecutions");
        }
    }
}
