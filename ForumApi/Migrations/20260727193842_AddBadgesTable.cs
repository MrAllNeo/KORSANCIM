using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForumApi.Migrations
{
    /// <inheritdoc />
    public partial class AddBadgesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BadgeId",
                table: "Users",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Badges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Icon = table.Column<string>(type: "TEXT", nullable: true),
                    ColorTheme = table.Column<string>(type: "TEXT", nullable: false),
                    Shine = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Badges", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_BadgeId",
                table: "Users",
                column: "BadgeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Badges_BadgeId",
                table: "Users",
                column: "BadgeId",
                principalTable: "Badges",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // wwwroot/js/app.js'teki eski hardcoded USER_TIERS'ın (creator/claude)
            // DB karşılığı — görünüm birebir korunuyor (aynı ikon/tema/shine).
            migrationBuilder.Sql(
                "INSERT INTO Badges (Id, Name, Icon, ColorTheme, Shine, CreatedAt) VALUES " +
                "(1, 'CREATOR & FOUNDER', 'crown', 'gold', 1, datetime('now')), " +
                "(2, 'ASSISTANT OF CREATOR', 'sparkles', 'cyan', 1, datetime('now'));");

            migrationBuilder.Sql("UPDATE Users SET BadgeId = 1 WHERE lower(Username) = 'creator';");
            migrationBuilder.Sql("UPDATE Users SET BadgeId = 2 WHERE lower(Username) = 'claude';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Badges_BadgeId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Badges");

            migrationBuilder.DropIndex(
                name: "IX_Users_BadgeId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BadgeId",
                table: "Users");
        }
    }
}
