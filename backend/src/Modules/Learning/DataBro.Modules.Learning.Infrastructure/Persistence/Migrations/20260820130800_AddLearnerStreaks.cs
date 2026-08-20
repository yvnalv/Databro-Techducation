using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataBro.Modules.Learning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLearnerStreaks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "learner_streaks",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current = table.Column<int>(type: "integer", nullable: false),
                    longest = table.Column<int>(type: "integer", nullable: false),
                    last_active_on = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("pk_learner_streaks", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_learner_streaks_user",
                schema: "learning",
                table: "learner_streaks",
                column: "user_id",
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "learner_streaks",
                schema: "learning");
        }
    }
}
