using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ForumApi.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoriesAndUserIdColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Topics",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "TopicLikes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Comments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "CommentLikes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Slug = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Icon = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "Description", "DisplayOrder", "Icon", "Name", "Slug" },
                values: new object[,]
                {
                    { 1, "C++, C#, Python, web ve kodlama dünyası", 1, "code", "Yazılım & Kodlama", "yazilim-kodlama" },
                    { 2, "Bilgisayar toplama, işletim sistemi, ağ ve sunucu", 2, "cpu", "Donanım & Sistem", "donanim-sistem" },
                    { 3, "Serbest kürsü — teknoloji dışı her şey", 3, "coffee", "Geyik & Sohbet", "geyik-sohbet" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Topics_CategoryId",
                table: "Topics",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug",
                table: "Categories",
                column: "Slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Topics_Categories_CategoryId",
                table: "Topics",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // ── Veri taşıma ────────────────────────────────────────────
            // Yazar/beğenen bilgisi bugüne kadar kullanıcı adı string'i olarak
            // tutuluyordu. Aşama 2'de bu sütunlar düşürüleceği için önce
            // UserId'leri dolduruyoruz.
            migrationBuilder.Sql(@"
                UPDATE Topics SET UserId = (
                    SELECT u.Id FROM Users u WHERE lower(u.Username) = lower(Topics.AuthorUsername));
                UPDATE Comments SET UserId = (
                    SELECT u.Id FROM Users u WHERE lower(u.Username) = lower(Comments.AuthorUsername));
                UPDATE TopicLikes SET UserId = (
                    SELECT u.Id FROM Users u WHERE lower(u.Username) = lower(TopicLikes.Username));
                UPDATE CommentLikes SET UserId = (
                    SELECT u.Id FROM Users u WHERE lower(u.Username) = lower(CommentLikes.Username));
            ");

            // Foreign key kurulamayacak kayıtları temizle: eşleşen kullanıcısı
            // olmayanlar ve konusu silinmiş yorumlar (uygulama katmanı bunları
            // eskiden geride bırakıyordu).
            migrationBuilder.Sql(@"
                DELETE FROM CommentLikes WHERE UserId IS NULL;
                DELETE FROM TopicLikes   WHERE UserId IS NULL;
                DELETE FROM Comments     WHERE UserId IS NULL;
                DELETE FROM Topics       WHERE UserId IS NULL;

                DELETE FROM CommentLikes WHERE CommentId NOT IN (SELECT Id FROM Comments);
                DELETE FROM TopicLikes   WHERE TopicId   NOT IN (SELECT Id FROM Topics);
                DELETE FROM Comments     WHERE TopicId   NOT IN (SELECT Id FROM Topics);

                UPDATE Topics SET CategoryId = 3 WHERE CategoryId NOT IN (SELECT Id FROM Categories);
            ");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Topics_Categories_CategoryId",
                table: "Topics");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Topics_CategoryId",
                table: "Topics");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Topics");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "TopicLikes");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "CommentLikes");
        }
    }
}
