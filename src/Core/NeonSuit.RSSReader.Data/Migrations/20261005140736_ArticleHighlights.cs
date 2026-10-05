using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeonSuit.RSSReader.Data.Migrations
{
    /// <inheritdoc />
    public partial class ArticleHighlights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HighlightColor",
                table: "Articles",
                type: "TEXT",
                maxLength: 9,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HighlightColor",
                table: "Articles");
        }
    }
}
