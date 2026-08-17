using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataBro.Modules.Content.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-edited: EF emitted DropPrimaryKey + AddPrimaryKey for what is only a change of
            // constraint *name*. PostgreSQL refuses to drop a primary key that foreign keys depend
            // on — article_versions and article_tags both reference these — so the generated pair
            // fails outright. Renaming in place is the same end state without touching dependents.
            //
            // The names go PascalCase because EFCore.NamingConventions cannot derive a table name
            // for a key declared on `ContentUnit`, which is abstract and has no table of its own.
            // Cosmetic, and recorded in STATUS rather than fought.
            migrationBuilder.Sql(
                """ALTER TABLE content.articles RENAME CONSTRAINT pk_articles TO "PK_articles";""");
            migrationBuilder.Sql(
                """ALTER TABLE content.article_versions RENAME CONSTRAINT pk_article_versions TO "PK_article_versions";""");

            migrationBuilder.RenameColumn(
                name: "article_id",
                schema: "content",
                table: "article_versions",
                newName: "content_unit_id");

            migrationBuilder.RenameIndex(
                name: "ix_article_versions_article_id_version",
                schema: "content",
                table: "article_versions",
                newName: "IX_article_versions_content_unit_id_version");

            migrationBuilder.CreateTable(
                name: "lesson_contents",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    draft_blocks = table.Column<string>(type: "jsonb", nullable: false),
                    published_blocks = table.Column<string>(type: "jsonb", nullable: true),
                    published_title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    published_summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    search_text = table.Column<string>(type: "text", nullable: false),
                    current_version = table.Column<int>(type: "integer", nullable: false),
                    reading_time_minutes = table.Column<int>(type: "integer", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    scheduled_for = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lesson_contents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lesson_content_versions",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    blocks = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lesson_content_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_lesson_content_versions_lesson_contents_content_unit_id",
                        column: x => x.content_unit_id,
                        principalSchema: "content",
                        principalTable: "lesson_contents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lesson_content_versions_content_unit_id_version",
                schema: "content",
                table: "lesson_content_versions",
                columns: new[] { "content_unit_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lesson_contents_slug",
                schema: "content",
                table: "lesson_contents",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lesson_contents_status_published_at",
                schema: "content",
                table: "lesson_contents",
                columns: new[] { "status", "published_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lesson_content_versions",
                schema: "content");

            migrationBuilder.DropTable(
                name: "lesson_contents",
                schema: "content");

            // Mirrors the hand-edited rename in Up: a drop would fail on the dependent foreign keys.
            migrationBuilder.Sql(
                """ALTER TABLE content.articles RENAME CONSTRAINT "PK_articles" TO pk_articles;""");
            migrationBuilder.Sql(
                """ALTER TABLE content.article_versions RENAME CONSTRAINT "PK_article_versions" TO pk_article_versions;""");

            migrationBuilder.RenameColumn(
                name: "content_unit_id",
                schema: "content",
                table: "article_versions",
                newName: "article_id");

            migrationBuilder.RenameIndex(
                name: "IX_article_versions_content_unit_id_version",
                schema: "content",
                table: "article_versions",
                newName: "ix_article_versions_article_id_version");

        }
    }
}
