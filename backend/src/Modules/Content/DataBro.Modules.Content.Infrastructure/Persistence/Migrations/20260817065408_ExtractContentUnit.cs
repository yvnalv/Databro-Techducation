using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataBro.Modules.Content.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtractContentUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_article_versions_articles_article_id",
                schema: "content",
                table: "article_versions");

            migrationBuilder.AddForeignKey(
                name: "fk_article_versions_articles_content_unit_id",
                schema: "content",
                table: "article_versions",
                column: "article_id",
                principalSchema: "content",
                principalTable: "articles",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_article_versions_articles_content_unit_id",
                schema: "content",
                table: "article_versions");

            migrationBuilder.AddForeignKey(
                name: "fk_article_versions_articles_article_id",
                schema: "content",
                table: "article_versions",
                column: "article_id",
                principalSchema: "content",
                principalTable: "articles",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
