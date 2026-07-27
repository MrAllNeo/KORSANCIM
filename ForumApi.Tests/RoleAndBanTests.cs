using System.Net;
using System.Net.Http.Json;
using ForumApi.Data;
using ForumApi.Models;
using Microsoft.Extensions.DependencyInjection;

namespace ForumApi.Tests
{
    public class RoleAndBanTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public RoleAndBanTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        // Kayıt akışında rol her zaman "User" atanır; testlerde admin/moderatör
        // senaryosu kurmak için veritabanına doğrudan yazıyoruz — tıpkı üretimde
        // ilk adminin CREATOR seed'i ile atanması gibi, self-servis yükseltme yok.
        private async Task SetRoleAsync(int userId, string role)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FindAsync(userId);
            user!.Role = role;
            await db.SaveChangesAsync();
        }

        [Fact]
        public async Task Regular_User_Cannot_Access_Admin_Endpoints()
        {
            var client = _factory.CreateClient();
            var auth = await TestHelpers.RegisterAndLoginAsync(client, TestHelpers.UniqueUsername("plain"));
            client.WithToken(auth.Token);

            var resp = await client.GetAsync("/api/admin/users");

            Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        }

        [Fact]
        public async Task Regular_User_Cannot_See_Stats_But_Moderator_Can()
        {
            var plainClient = _factory.CreateClient();
            var plainAuth = await TestHelpers.RegisterAndLoginAsync(plainClient, TestHelpers.UniqueUsername("statsplain"));
            plainClient.WithToken(plainAuth.Token);

            var forbidden = await plainClient.GetAsync("/api/admin/stats");
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

            var modClient = _factory.CreateClient();
            var modAuth = await TestHelpers.RegisterAndLoginAsync(modClient, TestHelpers.UniqueUsername("statsmod"));
            await SetRoleAsync(modAuth.UserId, Roles.Moderator);
            modAuth = await TestHelpers.LoginAsync(modClient, modAuth.Username, modAuth.Password);
            modClient.WithToken(modAuth.Token);

            var ok = await modClient.GetAsync("/api/admin/stats");
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

            var body = await ok.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.True(body.GetProperty("totalUsers").GetInt32() > 0);
            Assert.True(body.TryGetProperty("topicsByCategory", out _));
        }

        [Fact]
        public async Task Admin_Can_Ban_And_Unban_User()
        {
            var adminClient = _factory.CreateClient();
            var adminUsername = TestHelpers.UniqueUsername("admin");
            var adminAuth = await TestHelpers.RegisterAndLoginAsync(adminClient, adminUsername);
            await SetRoleAsync(adminAuth.UserId, Roles.Admin);
            // Rol değişikliği token'a işlensin diye yeniden giriş yapılır.
            adminAuth = await TestHelpers.LoginAsync(adminClient, adminUsername, adminAuth.Password);
            adminClient.WithToken(adminAuth.Token);

            var targetClient = _factory.CreateClient();
            var targetUsername = TestHelpers.UniqueUsername("target");
            var targetAuth = await TestHelpers.RegisterAndLoginAsync(targetClient, targetUsername);

            var banResp = await adminClient.PostAsJsonAsync($"/api/admin/users/{targetAuth.UserId}/ban", new { Reason = "Test için banlandı." });
            Assert.Equal(HttpStatusCode.OK, banResp.StatusCode);

            var loginAfterBan = await TestHelpers.LoginRawAsync(targetClient, targetUsername, targetAuth.Password);
            Assert.Equal(HttpStatusCode.Forbidden, loginAfterBan.StatusCode);

            var unbanResp = await adminClient.PostAsync($"/api/admin/users/{targetAuth.UserId}/unban", null);
            Assert.Equal(HttpStatusCode.OK, unbanResp.StatusCode);

            var loginAfterUnban = await TestHelpers.LoginRawAsync(targetClient, targetUsername, targetAuth.Password);
            Assert.Equal(HttpStatusCode.OK, loginAfterUnban.StatusCode);
        }

        [Fact]
        public async Task Banned_Users_Existing_Token_Is_Rejected_Immediately()
        {
            var adminClient = _factory.CreateClient();
            var adminUsername = TestHelpers.UniqueUsername("admin2");
            var adminAuth = await TestHelpers.RegisterAndLoginAsync(adminClient, adminUsername);
            await SetRoleAsync(adminAuth.UserId, Roles.Admin);
            adminAuth = await TestHelpers.LoginAsync(adminClient, adminUsername, adminAuth.Password);
            adminClient.WithToken(adminAuth.Token);

            var targetClient = _factory.CreateClient();
            var targetUsername = TestHelpers.UniqueUsername("target2");
            var targetAuth = await TestHelpers.RegisterAndLoginAsync(targetClient, targetUsername);
            targetClient.WithToken(targetAuth.Token); // ban öncesi alınmış token

            var banResp = await adminClient.PostAsJsonAsync($"/api/admin/users/{targetAuth.UserId}/ban", new { Reason = "Anlık test." });
            Assert.Equal(HttpStatusCode.OK, banResp.StatusCode);

            // targetClient hâlâ eski (ban öncesi) token'ı taşıyor; 12 saat dolmasını
            // beklemeden middleware bu isteği anında durdurmalı.
            var resp = await targetClient.GetAsync("/api/topics/999999/like");
            Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        }

        [Fact]
        public async Task Moderator_Cannot_Change_Roles_But_Can_Ban()
        {
            var modClient = _factory.CreateClient();
            var modUsername = TestHelpers.UniqueUsername("mod");
            var modAuth = await TestHelpers.RegisterAndLoginAsync(modClient, modUsername);
            await SetRoleAsync(modAuth.UserId, Roles.Moderator);
            modAuth = await TestHelpers.LoginAsync(modClient, modUsername, modAuth.Password);
            modClient.WithToken(modAuth.Token);

            var targetClient = _factory.CreateClient();
            var targetUsername = TestHelpers.UniqueUsername("target3");
            var targetAuth = await TestHelpers.RegisterAndLoginAsync(targetClient, targetUsername);

            var roleResp = await modClient.PutAsJsonAsync($"/api/admin/users/{targetAuth.UserId}/role", new { Role = Roles.Moderator });
            Assert.Equal(HttpStatusCode.Forbidden, roleResp.StatusCode);

            var banResp = await modClient.PostAsJsonAsync($"/api/admin/users/{targetAuth.UserId}/ban", new { Reason = "Moderatör banı." });
            Assert.Equal(HttpStatusCode.OK, banResp.StatusCode);
        }

        [Fact]
        public async Task Cannot_Ban_An_Admin_Or_Self()
        {
            var adminClient = _factory.CreateClient();
            var adminUsername = TestHelpers.UniqueUsername("admin3");
            var adminAuth = await TestHelpers.RegisterAndLoginAsync(adminClient, adminUsername);
            await SetRoleAsync(adminAuth.UserId, Roles.Admin);
            adminAuth = await TestHelpers.LoginAsync(adminClient, adminUsername, adminAuth.Password);
            adminClient.WithToken(adminAuth.Token);

            var selfBanResp = await adminClient.PostAsJsonAsync($"/api/admin/users/{adminAuth.UserId}/ban", new { Reason = "x" });
            Assert.Equal(HttpStatusCode.BadRequest, selfBanResp.StatusCode);

            var otherAdminClient = _factory.CreateClient();
            var otherAdminUsername = TestHelpers.UniqueUsername("admin4");
            var otherAdminAuth = await TestHelpers.RegisterAndLoginAsync(otherAdminClient, otherAdminUsername);
            await SetRoleAsync(otherAdminAuth.UserId, Roles.Admin);

            var banOtherAdminResp = await adminClient.PostAsJsonAsync($"/api/admin/users/{otherAdminAuth.UserId}/ban", new { Reason = "x" });
            Assert.Equal(HttpStatusCode.Forbidden, banOtherAdminResp.StatusCode);
        }

        [Fact]
        public async Task Admin_Cannot_Change_Roles_Only_Owner_Can()
        {
            var adminClient = _factory.CreateClient();
            var adminUsername = TestHelpers.UniqueUsername("admin5");
            var adminAuth = await TestHelpers.RegisterAndLoginAsync(adminClient, adminUsername);
            await SetRoleAsync(adminAuth.UserId, Roles.Admin);
            adminAuth = await TestHelpers.LoginAsync(adminClient, adminUsername, adminAuth.Password);
            adminClient.WithToken(adminAuth.Token);

            var targetClient = _factory.CreateClient();
            var targetUsername = TestHelpers.UniqueUsername("target5");
            var targetAuth = await TestHelpers.RegisterAndLoginAsync(targetClient, targetUsername);

            // Admin bile rol değiştiremez — adminler arası yetki çakışmasını önler.
            var adminAttempt = await adminClient.PutAsJsonAsync($"/api/admin/users/{targetAuth.UserId}/role", new { Role = Roles.Moderator });
            Assert.Equal(HttpStatusCode.Forbidden, adminAttempt.StatusCode);

            var ownerClient = _factory.CreateClient();
            var ownerUsername = TestHelpers.UniqueUsername("owner1");
            var ownerAuth = await TestHelpers.RegisterAndLoginAsync(ownerClient, ownerUsername);
            await SetRoleAsync(ownerAuth.UserId, Roles.Owner);
            ownerAuth = await TestHelpers.LoginAsync(ownerClient, ownerUsername, ownerAuth.Password);
            ownerClient.WithToken(ownerAuth.Token);

            var ownerAttempt = await ownerClient.PutAsJsonAsync($"/api/admin/users/{targetAuth.UserId}/role", new { Role = Roles.Moderator });
            Assert.Equal(HttpStatusCode.OK, ownerAttempt.StatusCode);

            // Yeni rol yalnızca bir sonraki girişte token'a işlenir.
            var reloginAuth = await TestHelpers.LoginAsync(targetClient, targetUsername, targetAuth.Password);
            targetClient.WithToken(reloginAuth.Token);

            var modOnlyResp = await targetClient.GetAsync("/api/admin/users");
            Assert.Equal(HttpStatusCode.OK, modOnlyResp.StatusCode);
        }

        [Fact]
        public async Task Owner_Cannot_Assign_Owner_Role_Or_Change_Owners_Role()
        {
            var ownerClient = _factory.CreateClient();
            var ownerUsername = TestHelpers.UniqueUsername("owner2");
            var ownerAuth = await TestHelpers.RegisterAndLoginAsync(ownerClient, ownerUsername);
            await SetRoleAsync(ownerAuth.UserId, Roles.Owner);
            ownerAuth = await TestHelpers.LoginAsync(ownerClient, ownerUsername, ownerAuth.Password);
            ownerClient.WithToken(ownerAuth.Token);

            var targetClient = _factory.CreateClient();
            var targetUsername = TestHelpers.UniqueUsername("target6");
            var targetAuth = await TestHelpers.RegisterAndLoginAsync(targetClient, targetUsername);

            // "Owner" atanabilir roller listesinde yok — tekil, yalnızca migration ile atanır.
            var assignOwnerResp = await ownerClient.PutAsJsonAsync($"/api/admin/users/{targetAuth.UserId}/role", new { Role = Roles.Owner });
            Assert.Equal(HttpStatusCode.BadRequest, assignOwnerResp.StatusCode);

            var secondOwnerClient = _factory.CreateClient();
            var secondOwnerUsername = TestHelpers.UniqueUsername("owner3");
            var secondOwnerAuth = await TestHelpers.RegisterAndLoginAsync(secondOwnerClient, secondOwnerUsername);
            await SetRoleAsync(secondOwnerAuth.UserId, Roles.Owner);

            var changeOtherOwnerResp = await ownerClient.PutAsJsonAsync($"/api/admin/users/{secondOwnerAuth.UserId}/role", new { Role = Roles.User });
            Assert.Equal(HttpStatusCode.Forbidden, changeOtherOwnerResp.StatusCode);
        }

        [Fact]
        public async Task Cannot_Ban_An_Owner()
        {
            var adminClient = _factory.CreateClient();
            var adminUsername = TestHelpers.UniqueUsername("admin6");
            var adminAuth = await TestHelpers.RegisterAndLoginAsync(adminClient, adminUsername);
            await SetRoleAsync(adminAuth.UserId, Roles.Admin);
            adminAuth = await TestHelpers.LoginAsync(adminClient, adminUsername, adminAuth.Password);
            adminClient.WithToken(adminAuth.Token);

            var ownerClient = _factory.CreateClient();
            var ownerUsername = TestHelpers.UniqueUsername("owner4");
            var ownerAuth = await TestHelpers.RegisterAndLoginAsync(ownerClient, ownerUsername);
            await SetRoleAsync(ownerAuth.UserId, Roles.Owner);

            var banResp = await adminClient.PostAsJsonAsync($"/api/admin/users/{ownerAuth.UserId}/ban", new { Reason = "x" });
            Assert.Equal(HttpStatusCode.Forbidden, banResp.StatusCode);
        }

        [Fact]
        public async Task Admin_Can_Edit_Others_Topic_But_Moderator_Cannot()
        {
            var ownerTopicClient = _factory.CreateClient();
            var topicOwnerAuth = await TestHelpers.RegisterAndLoginAsync(ownerTopicClient, TestHelpers.UniqueUsername("topicowner"));
            ownerTopicClient.WithToken(topicOwnerAuth.Token);
            var topicId = await TestHelpers.CreateTopicAsync(ownerTopicClient);

            var modClient = _factory.CreateClient();
            var modAuth = await TestHelpers.RegisterAndLoginAsync(modClient, TestHelpers.UniqueUsername("modeditor"));
            await SetRoleAsync(modAuth.UserId, Roles.Moderator);
            modAuth = await TestHelpers.LoginAsync(modClient, modAuth.Username, modAuth.Password);
            modClient.WithToken(modAuth.Token);

            var modEditResp = await modClient.PutAsJsonAsync($"/api/admin/topics/{topicId}",
                new { Title = "Moderatör düzenlemesi", CategoryId = 1, Content = "İçerik." });
            Assert.Equal(HttpStatusCode.Forbidden, modEditResp.StatusCode);

            var adminClient = _factory.CreateClient();
            var adminAuth = await TestHelpers.RegisterAndLoginAsync(adminClient, TestHelpers.UniqueUsername("admineditor"));
            await SetRoleAsync(adminAuth.UserId, Roles.Admin);
            adminAuth = await TestHelpers.LoginAsync(adminClient, adminAuth.Username, adminAuth.Password);
            adminClient.WithToken(adminAuth.Token);

            var adminEditResp = await adminClient.PutAsJsonAsync($"/api/admin/topics/{topicId}",
                new { Title = "Admin düzenlemesi", CategoryId = 1, Content = "Düzenlenmiş içerik." });
            Assert.Equal(HttpStatusCode.OK, adminEditResp.StatusCode);
        }
    }
}
