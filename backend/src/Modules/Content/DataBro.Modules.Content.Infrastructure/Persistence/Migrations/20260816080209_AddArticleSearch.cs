using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace DataBro.Modules.Content.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArticleSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.AddColumn<string>(
                name: "search_text",
                schema: "content",
                table: "articles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "search_vector",
                schema: "content",
                table: "articles",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "setweight(to_tsvector(CASE WHEN locale = 'id' THEN 'indonesian'::regconfig ELSE 'english'::regconfig END, coalesce(title, '')), 'A') ||\r\nsetweight(to_tsvector(CASE WHEN locale = 'id' THEN 'indonesian'::regconfig ELSE 'english'::regconfig END, coalesce(summary, '')), 'B') ||\r\nsetweight(to_tsvector(CASE WHEN locale = 'id' THEN 'indonesian'::regconfig ELSE 'english'::regconfig END, coalesce(search_text, '')), 'C')",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "ix_articles_search_vector",
                schema: "content",
                table: "articles",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_articles_search_vector",
                schema: "content",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "search_vector",
                schema: "content",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "search_text",
                schema: "content",
                table: "articles");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");
        }
    }
}
