using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForumApi.Migrations
{
    /// <inheritdoc />
    public partial class LinkContentToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Topics_Categories_CategoryId",
                table: "Topics");

            migrationBuilder.DropIndex(
                name: "IX_TopicLikes_TopicId_Username",
                table: "TopicLikes");

            migrationBuilder.DropIndex(
                name: "IX_CommentLikes_CommentId_Username",
                table: "CommentLikes");

            migrationBuilder.DropColumn(
                name: "AuthorUsername",
                table: "Topics");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "TopicLikes");

            migrationBuilder.DropColumn(
                name: "AuthorUsername",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "CommentLikes");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "Topics",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "TopicLikes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "Comments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "CommentLikes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Topics_UserId",
                table: "Topics",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TopicLikes_TopicId_UserId",
                table: "TopicLikes",
                columns: new[] { "TopicId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TopicLikes_UserId",
                table: "TopicLikes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_UserId",
                table: "Comments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommentLikes_CommentId_UserId",
                table: "CommentLikes",
                columns: new[] { "CommentId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommentLikes_UserId",
                table: "CommentLikes",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_CommentLikes_Comments_CommentId",
                table: "CommentLikes",
                column: "CommentId",
                principalTable: "Comments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CommentLikes_Users_UserId",
                table: "CommentLikes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Users_UserId",
                table: "Comments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TopicLikes_Topics_TopicId",
                table: "TopicLikes",
                column: "TopicId",
                principalTable: "Topics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TopicLikes_Users_UserId",
                table: "TopicLikes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Topics_Categories_CategoryId",
                table: "Topics",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Topics_Users_UserId",
                table: "Topics",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CommentLikes_Comments_CommentId",
                table: "CommentLikes");

            migrationBuilder.DropForeignKey(
                name: "FK_CommentLikes_Users_UserId",
                table: "CommentLikes");

            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Users_UserId",
                table: "Comments");

            migrationBuilder.DropForeignKey(
                name: "FK_TopicLikes_Topics_TopicId",
                table: "TopicLikes");

            migrationBuilder.DropForeignKey(
                name: "FK_TopicLikes_Users_UserId",
                table: "TopicLikes");

            migrationBuilder.DropForeignKey(
                name: "FK_Topics_Categories_CategoryId",
                table: "Topics");

            migrationBuilder.DropForeignKey(
                name: "FK_Topics_Users_UserId",
                table: "Topics");

            migrationBuilder.DropIndex(
                name: "IX_Topics_UserId",
                table: "Topics");

            migrationBuilder.DropIndex(
                name: "IX_TopicLikes_TopicId_UserId",
                table: "TopicLikes");

            migrationBuilder.DropIndex(
                name: "IX_TopicLikes_UserId",
                table: "TopicLikes");

            migrationBuilder.DropIndex(
                name: "IX_Comments_UserId",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_CommentLikes_CommentId_UserId",
                table: "CommentLikes");

            migrationBuilder.DropIndex(
                name: "IX_CommentLikes_UserId",
                table: "CommentLikes");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "Topics",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "AuthorUsername",
                table: "Topics",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "TopicLikes",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "TopicLikes",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "Comments",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "AuthorUsername",
                table: "Comments",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "CommentLikes",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "CommentLikes",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

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

            migrationBuilder.AddForeignKey(
                name: "FK_Topics_Categories_CategoryId",
                table: "Topics",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
