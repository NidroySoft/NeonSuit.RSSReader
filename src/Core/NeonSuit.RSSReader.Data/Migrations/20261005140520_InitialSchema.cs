using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeonSuit.RSSReader.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ParentCategoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 7, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Categories_Categories_ParentCategoryId",
                        column: x => x.ParentCategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Rules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Target = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Operator = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Value2 = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsCaseSensitive = table.Column<bool>(type: "INTEGER", nullable: false),
                    RegexPattern = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ConditionGroup = table.Column<int>(type: "INTEGER", nullable: false),
                    NextConditionOperator = table.Column<int>(type: "INTEGER", nullable: false),
                    UsesAdvancedConditions = table.Column<bool>(type: "INTEGER", nullable: false),
                    Scope = table.Column<int>(type: "INTEGER", nullable: false),
                    FeedIds = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    CategoryIds = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ActionType = table.Column<int>(type: "INTEGER", nullable: false),
                    TagIds = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    SoundPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    NotificationTemplate = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    NotificationPriority = table.Column<int>(type: "INTEGER", nullable: false),
                    HighlightColor = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    StopOnMatch = table.Column<bool>(type: "INTEGER", nullable: false),
                    OnlyNewArticles = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MatchCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastMatchDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Color = table.Column<string>(type: "TEXT", maxLength: 9, nullable: false),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    IsPinned = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsVisible = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UsageCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Feeds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    WebsiteUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    IconUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    Language = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    UpdateFrequency = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NextUpdateSchedule = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    FailureCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    ETag = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    LastModifiedHeader = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ArticleRetentionDays = table.Column<int>(type: "INTEGER", nullable: true),
                    LastFullSync = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TotalArticleCount = table.Column<int>(type: "INTEGER", nullable: false),
                    IsPodcastFeed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Feeds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Feeds_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RuleConditions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RuleId = table.Column<int>(type: "INTEGER", nullable: false),
                    GroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    CombineWithNext = table.Column<int>(type: "INTEGER", nullable: false),
                    Field = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Operator = table.Column<int>(type: "INTEGER", nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Value2 = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsCaseSensitive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Negate = table.Column<bool>(type: "INTEGER", nullable: false),
                    RegexPattern = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    DateFormat = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleConditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuleConditions_Rules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "Rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Articles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Guid = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    ContentHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    FeedId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 1200, nullable: false),
                    Link = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    Content = table.Column<string>(type: "TEXT", nullable: true),
                    Author = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    PublishedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AddedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ImageUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    Categories = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Language = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    EnclosureUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    EnclosureType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    EnclosureLength = table.Column<long>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsStarred = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsNotified = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProcessedByRules = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReadPercentage = table.Column<int>(type: "INTEGER", nullable: false),
                    LastReadAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Articles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Articles_Feeds_FeedId",
                        column: x => x.FeedId,
                        principalTable: "Feeds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArticleTags",
                columns: table => new
                {
                    ArticleId = table.Column<int>(type: "INTEGER", nullable: false),
                    TagId = table.Column<int>(type: "INTEGER", nullable: false),
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    AppliedBy = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RuleId = table.Column<int>(type: "INTEGER", nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleTags", x => new { x.ArticleId, x.TagId });
                    table.ForeignKey(
                        name: "FK_ArticleTags_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArticleTags_Rules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "Rules",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ArticleTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificationLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ArticleId = table.Column<int>(type: "INTEGER", nullable: false),
                    RuleId = table.Column<int>(type: "INTEGER", nullable: true),
                    NotificationType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Channel = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SoundPlayed = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Duration = table.Column<int>(type: "INTEGER", nullable: false),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Delivered = table.Column<bool>(type: "INTEGER", nullable: false),
                    Error = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ActionAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Action = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationLogs_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotificationLogs_Rules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "Rules",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Article_ContentHash",
                table: "Articles",
                column: "ContentHash",
                filter: "[ContentHash] IS NOT NULL AND [ContentHash] != ''");

            migrationBuilder.CreateIndex(
                name: "IX_Article_FeedId_PublishedDate",
                table: "Articles",
                columns: new[] { "FeedId", "PublishedDate" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Article_IsNotified",
                table: "Articles",
                column: "IsNotified",
                filter: "[IsNotified] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Article_PublishedDate",
                table: "Articles",
                column: "PublishedDate",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_ArticleTag_ArticleId_TagId",
                table: "ArticleTags",
                columns: new[] { "ArticleId", "TagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArticleTags_RuleId",
                table: "ArticleTags",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleTags_TagId",
                table: "ArticleTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_Category_ParentId_Name",
                table: "Categories",
                columns: new[] { "ParentCategoryId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Feed_CategoryId",
                table: "Feeds",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Feed_IsActive_NextUpdateSchedule",
                table: "Feeds",
                columns: new[] { "IsActive", "NextUpdateSchedule" },
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Feed_Url",
                table: "Feeds",
                column: "Url",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLog_ArticleId",
                table: "NotificationLogs",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLog_SentAt",
                table: "NotificationLogs",
                column: "SentAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_RuleId",
                table: "NotificationLogs",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_RuleCondition_RuleId",
                table: "RuleConditions",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Rule_IsEnabled_Priority",
                table: "Rules",
                columns: new[] { "IsEnabled", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Name",
                table: "Tags",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_Key",
                table: "UserPreferences",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArticleTags");

            migrationBuilder.DropTable(
                name: "NotificationLogs");

            migrationBuilder.DropTable(
                name: "RuleConditions");

            migrationBuilder.DropTable(
                name: "UserPreferences");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "Articles");

            migrationBuilder.DropTable(
                name: "Rules");

            migrationBuilder.DropTable(
                name: "Feeds");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}
