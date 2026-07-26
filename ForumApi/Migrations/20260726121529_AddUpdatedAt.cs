using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForumApi.Migrations
{
    /// <inheritdoc />
    public partial class AddUpdatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Topics",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Comments",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Topics");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Comments");
        }
    }
}
