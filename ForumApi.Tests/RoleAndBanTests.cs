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
        public async Task Admin_Can_Change_User_Role()
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

            var roleResp = await adminClient.PutAsJsonAsync($"/api/admin/users/{targetAuth.UserId}/role", new { Role = Roles.Moderator });
            Assert.Equal(HttpStatusCode.OK, roleResp.StatusCode);

            // Yeni rol yalnızca bir sonraki girişte token'a işlenir.
            var reloginAuth = await TestHelpers.LoginAsync(targetClient, targetUsername, targetAuth.Password);
            targetClient.WithToken(reloginAuth.Token);

            var modOnlyResp = await targetClient.GetAsync("/api/admin/users");
            Assert.Equal(HttpStatusCode.OK, modOnlyResp.StatusCode);
        }
    }
}
