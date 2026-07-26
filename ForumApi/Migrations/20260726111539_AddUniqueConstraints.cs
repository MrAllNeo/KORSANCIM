using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForumApi.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TopicLikes_TopicId_Username",
                table: "TopicLikes",
                columns: new[] { "TopicId", "Username" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommentLikes_CommentId_Username",
                table: "CommentLikes",
                columns: new[] { "CommentId", "Username" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_TopicLikes_TopicId_Username",
                table: "TopicLikes");

            migrationBuilder.DropIndex(
                name: "IX_CommentLikes_CommentId_Username",
                table: "CommentLikes");
        }
    }
}
