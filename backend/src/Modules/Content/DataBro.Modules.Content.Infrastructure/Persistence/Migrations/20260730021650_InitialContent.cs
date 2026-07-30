using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataBro.Modules.Content.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "content");

            migrationBuilder.CreateTable(
                name: "articles",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    visibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    locale = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    translation_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    draft_blocks = table.Column<string>(type: "jsonb", nullable: false),
                    published_blocks = table.Column<string>(type: "jsonb", nullable: true),
                    current_version = table.Column<int>(type: "integer", nullable: false),
                    reading_time_minutes = table.Column<int>(type: "integer", nullable: false),
                    seo = table.Column<string>(type: "jsonb", nullable: false),
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
                    table.PrimaryKey("pk_articles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "article_versions",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    article_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_article_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_article_versions_articles_article_id",
                        column: x => x.article_id,
                        principalSchema: "content",
                        principalTable: "articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_article_versions_article_id_version",
                schema: "content",
                table: "article_versions",
                columns: new[] { "article_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_articles_category_id",
                schema: "content",
                table: "articles",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_articles_slug",
                schema: "content",
                table: "articles",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_articles_status_published_at",
                schema: "content",
                table: "articles",
                columns: new[] { "status", "published_at" });

            migrationBuilder.CreateIndex(
                name: "ix_articles_translation_group_id",
                schema: "content",
                table: "articles",
                column: "translation_group_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "article_versions",
                schema: "content");

            migrationBuilder.DropTable(
                name: "articles",
                schema: "content");
        }
    }
}
