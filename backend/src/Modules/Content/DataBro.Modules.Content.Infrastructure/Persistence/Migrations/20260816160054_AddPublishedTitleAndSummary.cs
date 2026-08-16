using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace DataBro.Modules.Content.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPublishedTitleAndSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "published_summary",
                schema: "content",
                table: "articles",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "published_title",
                schema: "content",
                table: "articles",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            // Backfill before the search vector is redefined below, so it is recomputed with these
            // values present rather than against two columns of NULL.
            //
            // A straight copy is correct here and only here: until this migration the draft title
            // *was* the published title — there was nowhere else for it to live. Every article that
            // has ever been published is therefore carrying its published title in `title`. Anything
            // never published stays NULL, which is exactly right.
            migrationBuilder.Sql("""
                UPDATE content.articles
                SET published_title = title,
                    published_summary = summary
                WHERE published_at IS NOT NULL;
                """);

            migrationBuilder.AlterColumn<NpgsqlTsVector>(
                name: "search_vector",
                schema: "content",
                table: "articles",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "setweight(to_tsvector(CASE WHEN locale = 'id' THEN 'indonesian'::regconfig ELSE 'english'::regconfig END, coalesce(published_title, '')), 'A') ||\r\nsetweight(to_tsvector(CASE WHEN locale = 'id' THEN 'indonesian'::regconfig ELSE 'english'::regconfig END, coalesce(published_summary, '')), 'B') ||\r\nsetweight(to_tsvector(CASE WHEN locale = 'id' THEN 'indonesian'::regconfig ELSE 'english'::regconfig END, coalesce(search_text, '')), 'C')",
                stored: true,
                oldClrType: typeof(NpgsqlTsVector),
                oldType: "tsvector",
                oldNullable: true,
                oldComputedColumnSql: "setweight(to_tsvector(CASE WHEN locale = 'id' THEN 'indonesian'::regconfig ELSE 'english'::regconfig END, coalesce(title, '')), 'A') ||\r\nsetweight(to_tsvector(CASE WHEN locale = 'id' THEN 'indonesian'::regconfig ELSE 'english'::regconfig END, coalesce(summary, '')), 'B') ||\r\nsetweight(to_tsvector(CASE WHEN locale = 'id' THEN 'indonesian'::regconfig ELSE 'english'::regconfig END, coalesce(search_text, '')), 'C')",
                oldStored: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "published_summary",
                schema: "content",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "published_title",
                schema: "content",
                table: "articles");

            migrationBuilder.AlterColumn<NpgsqlTsVector>(
                name: "search_vector",
                schema: "content",
                table: "articles",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "setweight(to_tsvector(CASE WHEN locale = 'id' THEN 'indonesian'::regconfig ELSE 'english'::regconfig END, coalesce(title, '')), 'A') ||\r\nsetweight(to_tsvector(CASE WHEN locale = 'id' THEN 'indonesian'::regconfig ELSE 'english'::regconfig END, coalesce(summary, '')), 'B') ||\r\nsetweight(to_tsvector(CASE WHEN locale = 'id' THEN 'indonesian'::regconfig ELSE 'english'::regconfig END, coalesce(search_text, '')), 'C')",
                stored: true,
                oldClrType: typeof(NpgsqlTsVector),
                oldType: "tsvector",
                oldNullable: true,
                oldComputedColumnSql: "setweight(to_tsvector(CASE WHEN locale = 'id' THEN 'indonesian'::regconfig ELSE 'english'::regconfig END, coalesce(published_title, '')), 'A') ||\r\nsetweight(to_tsvector(CASE WHEN locale = 'id' THEN 'indonesian'::regconfig ELSE 'english'::regconfig END, coalesce(published_summary, '')), 'B') ||\r\nsetweight(to_tsvector(CASE WHEN locale = 'id' THEN 'indonesian'::regconfig ELSE 'english'::regconfig END, coalesce(search_text, '')), 'C')",
                oldStored: true);
        }
    }
}
