using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace DataBro.Modules.Content.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormaliseSearchVectorSql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<NpgsqlTsVector>(
                name: "search_vector",
                schema: "content",
                table: "articles",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "setweight(to_tsvector(CASE WHEN locale = 'id' THEN 'indonesian'::regconfig ELSE 'english'::regconfig END, coalesce(published_title, '')), 'A') || setweight(to_tsvector(CASE WHEN locale = 'id' THEN 'indonesian'::regconfig ELSE 'english'::regconfig END, coalesce(published_summary, '')), 'B') || setweight(to_tsvector(CASE WHEN locale = 'id' THEN 'indonesian'::regconfig ELSE 'english'::regconfig END, coalesce(search_text, '')), 'C')",
                stored: true,
                oldClrType: typeof(NpgsqlTsVector),
                oldType: "tsvector",
                oldNullable: true,
                oldComputedColumnSql: "setweight(to_tsvector(CASE WHEN locale = 'id' THEN 'indonesian'::regconfig ELSE 'english'::regconfig END, coalesce(published_title, '')), 'A') ||\r\nsetweight(to_tsvector(CASE WHEN locale = 'id' THEN 'indonesian'::regconfig ELSE 'english'::regconfig END, coalesce(published_summary, '')), 'B') ||\r\nsetweight(to_tsvector(CASE WHEN locale = 'id' THEN 'indonesian'::regconfig ELSE 'english'::regconfig END, coalesce(search_text, '')), 'C')",
                oldStored: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                oldComputedColumnSql: "setweight(to_tsvector(CASE WHEN locale = 'id' THEN 'indonesian'::regconfig ELSE 'english'::regconfig END, coalesce(published_title, '')), 'A') || setweight(to_tsvector(CASE WHEN locale = 'id' THEN 'indonesian'::regconfig ELSE 'english'::regconfig END, coalesce(published_summary, '')), 'B') || setweight(to_tsvector(CASE WHEN locale = 'id' THEN 'indonesian'::regconfig ELSE 'english'::regconfig END, coalesce(search_text, '')), 'C')",
                oldStored: true);
        }
    }
}
