using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace DataBro.Modules.Learning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "search_vector",
                schema: "learning",
                table: "courses",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "setweight(to_tsvector('english'::regconfig, coalesce(title, '')), 'A') || setweight(to_tsvector('english'::regconfig, coalesce(summary, '')), 'B')",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "ix_courses_search_vector",
                schema: "learning",
                table: "courses",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_courses_search_vector",
                schema: "learning",
                table: "courses");

            migrationBuilder.DropColumn(
                name: "search_vector",
                schema: "learning",
                table: "courses");
        }
    }
}
