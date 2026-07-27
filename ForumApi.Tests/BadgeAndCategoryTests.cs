using System.Net;
using System.Net.Http.Json;
using ForumApi.Data;
using ForumApi.Models;
using Microsoft.Extensions.DependencyInjection;

namespace ForumApi.Tests
{
    public class BadgeAndCategoryTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public BadgeAndCategoryTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task SetRoleAsync(int userId, string role)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FindAsync(userId);
            user!.Role = role;
            await db.SaveChangesAsync();
        }

        private async Task<HttpClient> LoggedInStaffAsync(string role, string prefix)
        {
            var client = _factory.CreateClient();
            var username = TestHelpers.UniqueUsername(prefix);
            var auth = await TestHelpers.RegisterAndLoginAsync(client, username);
            await SetRoleAsync(auth.UserId, role);
            auth = await TestHelpers.LoginAsync(client, username, auth.Password);
            client.WithToken(auth.Token);
            return client;
        }

        [Fact]
        public async Task Only_Owner_Can_Create_Update_Delete_Badge()
        {
            var adminClient = await LoggedInStaffAsync(Roles.Admin, "badgeadmin");
            var ownerClient = await LoggedInStaffAsync(Roles.Owner, "badgeowner");

            var adminCreate = await adminClient.PostAsJsonAsync("/api/admin/badges", new { Name = "Test Rozeti", Icon = "star", ColorTheme = BadgeThemes.Purple, Shine = false });
            Assert.Equal(HttpStatusCode.Forbidden, adminCreate.StatusCode);

            var ownerCreate = await ownerClient.PostAsJsonAsync("/api/admin/badges", new { Name = "Test Rozeti", Icon = "star", ColorTheme = BadgeThemes.Purple, Shine = false });
            Assert.Equal(HttpStatusCode.OK, ownerCreate.StatusCode);
            var created = await ownerCreate.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            var badgeId = created.GetProperty("badgeId").GetInt32();

            var ownerUpdate = await ownerClient.PutAsJsonAsync($"/api/admin/badges/{badgeId}", new { Name = "Güncellenmiş Rozet", Icon = "star", ColorTheme = BadgeThemes.Green, Shine = true });
            Assert.Equal(HttpStatusCode.OK, ownerUpdate.StatusCode);

            var adminDelete = await adminClient.DeleteAsync($"/api/admin/badges/{badgeId}");
            Assert.Equal(HttpStatusCode.Forbidden, adminDelete.StatusCode);

            var ownerDelete = await ownerClient.DeleteAsync($"/api/admin/badges/{badgeId}");
            Assert.Equal(HttpStatusCode.OK, ownerDelete.StatusCode);
        }

        [Fact]
        public async Task Invalid_Badge_Theme_Is_Rejected()
        {
            var ownerClient = await LoggedInStaffAsync(Roles.Owner, "badgeowner2");

            var resp = await ownerClient.PostAsJsonAsync("/api/admin/badges", new { Name = "Geçersiz Tema", Icon = "star", ColorTheme = "neon-pink", Shine = false });
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        [Fact]
        public async Task Admin_Can_Assign_Badge_But_Moderator_Cannot()
        {
            var ownerClient = await LoggedInStaffAsync(Roles.Owner, "badgeowner3");
            var createResp = await ownerClient.PostAsJsonAsync("/api/admin/badges", new { Name = "Atanabilir Rozet", Icon = "star", ColorTheme = BadgeThemes.Cyan, Shine = false });
            var created = await createResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            var badgeId = created.GetProperty("badgeId").GetInt32();

            var targetClient = _factory.CreateClient();
            var targetAuth = await TestHelpers.RegisterAndLoginAsync(targetClient, TestHelpers.UniqueUsername("badgetarget"));

            var modClient = await LoggedInStaffAsync(Roles.Moderator, "badgemod");
            var modAssign = await modClient.PutAsJsonAsync($"/api/admin/users/{targetAuth.UserId}/badge", new { BadgeId = badgeId });
            Assert.Equal(HttpStatusCode.Forbidden, modAssign.StatusCode);

            var adminClient = await LoggedInStaffAsync(Roles.Admin, "badgeadmin2");
            var adminAssign = await adminClient.PutAsJsonAsync($"/api/admin/users/{targetAuth.UserId}/badge", new { BadgeId = badgeId });
            Assert.Equal(HttpStatusCode.OK, adminAssign.StatusCode);

            var removeAssign = await adminClient.PutAsJsonAsync($"/api/admin/users/{targetAuth.UserId}/badge", new { BadgeId = (int?)null });
            Assert.Equal(HttpStatusCode.OK, removeAssign.StatusCode);
        }

        [Fact]
        public async Task Assigning_Nonexistent_Badge_Returns_BadRequest()
        {
            var adminClient = await LoggedInStaffAsync(Roles.Admin, "badgeadmin3");
            var targetClient = _factory.CreateClient();
            var targetAuth = await TestHelpers.RegisterAndLoginAsync(targetClient, TestHelpers.UniqueUsername("badgetarget2"));

            var resp = await adminClient.PutAsJsonAsync($"/api/admin/users/{targetAuth.UserId}/badge", new { BadgeId = 999999 });
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        [Fact]
        public async Task Admin_Can_Manage_Categories_But_Moderator_Cannot()
        {
            var modClient = await LoggedInStaffAsync(Roles.Moderator, "catmod");
            var adminClient = await LoggedInStaffAsync(Roles.Admin, "catadmin");

            var modCreate = await modClient.PostAsJsonAsync("/api/admin/categories", new { Name = "Deneme Kategorisi", Description = "Açıklama.", Icon = "hash", DisplayOrder = 10 });
            Assert.Equal(HttpStatusCode.Forbidden, modCreate.StatusCode);

            var adminCreate = await adminClient.PostAsJsonAsync("/api/admin/categories", new { Name = "Deneme Kategorisi", Description = "Açıklama.", Icon = "hash", DisplayOrder = 10 });
            Assert.Equal(HttpStatusCode.OK, adminCreate.StatusCode);
            var created = await adminCreate.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            var categoryId = created.GetProperty("categoryId").GetInt32();

            var adminUpdate = await adminClient.PutAsJsonAsync($"/api/admin/categories/{categoryId}", new { Name = "Güncellenmiş Kategori", Description = "Yeni açıklama.", Icon = "hash", DisplayOrder = 11 });
            Assert.Equal(HttpStatusCode.OK, adminUpdate.StatusCode);

            var adminDelete = await adminClient.DeleteAsync($"/api/admin/categories/{categoryId}");
            Assert.Equal(HttpStatusCode.OK, adminDelete.StatusCode);
        }

        [Fact]
        public async Task Cannot_Delete_Category_That_Has_Topics()
        {
            var adminClient = await LoggedInStaffAsync(Roles.Admin, "catadmin2");

            var createResp = await adminClient.PostAsJsonAsync("/api/admin/categories", new { Name = "Dolu Kategori", Description = "Açıklama.", Icon = "hash", DisplayOrder = 20 });
            var created = await createResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            var categoryId = created.GetProperty("categoryId").GetInt32();

            var topicClient = _factory.CreateClient();
            var topicAuth = await TestHelpers.RegisterAndLoginAsync(topicClient, TestHelpers.UniqueUsername("cattopicowner"));
            topicClient.WithToken(topicAuth.Token);
            await TestHelpers.CreateTopicAsync(topicClient, categoryId: categoryId);

            var deleteResp = await adminClient.DeleteAsync($"/api/admin/categories/{categoryId}");
            Assert.Equal(HttpStatusCode.BadRequest, deleteResp.StatusCode);
        }

        [Fact]
        public async Task Duplicate_Category_Name_Is_Rejected()
        {
            var adminClient = await LoggedInStaffAsync(Roles.Admin, "catadmin3");

            var dup = await adminClient.PostAsJsonAsync("/api/admin/categories", new { Name = "Yazılım & Kodlama", Description = "Açıklama.", Icon = "hash", DisplayOrder = 1 });
            Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);
        }
    }
}
