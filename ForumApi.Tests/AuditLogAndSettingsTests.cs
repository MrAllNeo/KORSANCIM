using System.Net;
using System.Net.Http.Json;
using ForumApi.Data;
using ForumApi.Models;
using Microsoft.Extensions.DependencyInjection;

namespace ForumApi.Tests
{
    public class AuditLogAndSettingsTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public AuditLogAndSettingsTests(CustomWebApplicationFactory factory)
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

        // Diğer testler varsayılan (açık kayıt, bakım kapalı) ayarları
        // bekliyor; her stateful test kendi sonunda bunu geri yükler.
        private async Task ResetSettingsAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var settings = await db.SiteSettings.FindAsync(1);
            if (settings != null)
            {
                settings.RegistrationOpen = true;
                settings.MaintenanceMode = false;
                settings.AnnouncementText = null;
                await db.SaveChangesAsync();
            }
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
        public async Task Only_Owner_Can_View_Audit_Log()
        {
            var adminClient = await LoggedInStaffAsync(Roles.Admin, "auditadmin");
            var ownerClient = await LoggedInStaffAsync(Roles.Owner, "auditowner");

            var adminResp = await adminClient.GetAsync("/api/admin/audit-logs");
            Assert.Equal(HttpStatusCode.Forbidden, adminResp.StatusCode);

            var ownerResp = await ownerClient.GetAsync("/api/admin/audit-logs");
            Assert.Equal(HttpStatusCode.OK, ownerResp.StatusCode);
        }

        [Fact]
        public async Task Ban_Action_Is_Recorded_In_Audit_Log()
        {
            var adminClient = await LoggedInStaffAsync(Roles.Admin, "auditadmin2");
            var ownerClient = await LoggedInStaffAsync(Roles.Owner, "auditowner2");

            var targetClient = _factory.CreateClient();
            var targetAuth = await TestHelpers.RegisterAndLoginAsync(targetClient, TestHelpers.UniqueUsername("audittarget"));

            var banResp = await adminClient.PostAsJsonAsync($"/api/admin/users/{targetAuth.UserId}/ban", new { Reason = "denetim testi" });
            Assert.Equal(HttpStatusCode.OK, banResp.StatusCode);

            var logsResp = await ownerClient.GetAsync("/api/admin/audit-logs?action=BanUser");
            Assert.Equal(HttpStatusCode.OK, logsResp.StatusCode);

            var body = await logsResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            var items = body.GetProperty("items").EnumerateArray().ToList();
            Assert.Contains(items, item => item.GetProperty("targetId").GetInt32() == targetAuth.UserId);
        }

        [Fact]
        public async Task ChangeRole_Requires_Correct_Current_Password()
        {
            var ownerClient = await LoggedInStaffAsync(Roles.Owner, "reauthowner");

            var targetClient = _factory.CreateClient();
            var targetAuth = await TestHelpers.RegisterAndLoginAsync(targetClient, TestHelpers.UniqueUsername("reauthtarget"));

            var wrongPasswordResp = await ownerClient.PutAsJsonAsync(
                $"/api/admin/users/{targetAuth.UserId}/role",
                new { Role = Roles.Moderator, CurrentPassword = "yanlis-sifre-123" });
            Assert.Equal(HttpStatusCode.Unauthorized, wrongPasswordResp.StatusCode);

            var missingPasswordResp = await ownerClient.PutAsJsonAsync(
                $"/api/admin/users/{targetAuth.UserId}/role",
                new { Role = Roles.Moderator });
            Assert.Equal(HttpStatusCode.BadRequest, missingPasswordResp.StatusCode);
        }

        [Fact]
        public async Task Only_Owner_Can_Update_Settings()
        {
            var adminClient = await LoggedInStaffAsync(Roles.Admin, "settingsadmin");
            var ownerClient = await LoggedInStaffAsync(Roles.Owner, "settingsowner");

            var adminResp = await adminClient.PutAsJsonAsync("/api/admin/settings",
                new { RegistrationOpen = true, MaintenanceMode = false, AnnouncementText = (string?)null });
            Assert.Equal(HttpStatusCode.Forbidden, adminResp.StatusCode);

            var ownerResp = await ownerClient.PutAsJsonAsync("/api/admin/settings",
                new { RegistrationOpen = true, MaintenanceMode = false, AnnouncementText = "test duyurusu" });
            Assert.Equal(HttpStatusCode.OK, ownerResp.StatusCode);

            var publicResp = await _factory.CreateClient().GetAsync("/api/settings");
            var publicBody = await publicResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.Equal("test duyurusu", publicBody.GetProperty("announcementText").GetString());

            await ResetSettingsAsync();
        }

        [Fact]
        public async Task Closed_Registration_Rejects_New_Signups()
        {
            var ownerClient = await LoggedInStaffAsync(Roles.Owner, "regowner");

            var closeResp = await ownerClient.PutAsJsonAsync("/api/admin/settings",
                new { RegistrationOpen = false, MaintenanceMode = false, AnnouncementText = (string?)null });
            Assert.Equal(HttpStatusCode.OK, closeResp.StatusCode);

            var newUserClient = _factory.CreateClient();
            var username = TestHelpers.UniqueUsername("blockeduser");
            var registerResp = await newUserClient.PostAsJsonAsync("/api/auth/register",
                new { Username = username, Email = $"{username}@test.local", Password = "Password123!" });
            Assert.Equal(HttpStatusCode.Forbidden, registerResp.StatusCode);

            await ResetSettingsAsync();

            var reopenedResp = await newUserClient.PostAsJsonAsync("/api/auth/register",
                new { Username = username, Email = $"{username}@test.local", Password = "Password123!" });
            Assert.Equal(HttpStatusCode.OK, reopenedResp.StatusCode);
        }

        [Fact]
        public async Task Maintenance_Mode_Blocks_NonOwner_Api_But_Not_Owner_Or_Settings()
        {
            var ownerClient = await LoggedInStaffAsync(Roles.Owner, "maintowner");
            var plainClient = await LoggedInStaffAsync(Roles.User, "maintplain");

            var onResp = await ownerClient.PutAsJsonAsync("/api/admin/settings",
                new { RegistrationOpen = true, MaintenanceMode = true, AnnouncementText = (string?)null });
            Assert.Equal(HttpStatusCode.OK, onResp.StatusCode);

            var blockedResp = await plainClient.GetAsync("/api/topics");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, blockedResp.StatusCode);

            var settingsStillWorks = await plainClient.GetAsync("/api/settings");
            Assert.Equal(HttpStatusCode.OK, settingsStillWorks.StatusCode);

            var ownerStillWorks = await ownerClient.GetAsync("/api/admin/stats");
            Assert.Equal(HttpStatusCode.OK, ownerStillWorks.StatusCode);

            await ResetSettingsAsync();

            var afterResetResp = await plainClient.GetAsync("/api/topics");
            Assert.Equal(HttpStatusCode.OK, afterResetResp.StatusCode);
        }

        [Fact]
        public async Task Admin_And_Owner_Get_Shorter_Token_Expiry_Than_Regular_User()
        {
            var plainClient = _factory.CreateClient();
            var plainUsername = TestHelpers.UniqueUsername("expiryplain");
            var plainAuth = await TestHelpers.RegisterAndLoginAsync(plainClient, plainUsername);

            var plainLoginResp = await TestHelpers.LoginRawAsync(plainClient, plainUsername, plainAuth.Password);
            var plainBody = await plainLoginResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.Equal(12, plainBody.GetProperty("expiresInHours").GetInt32());

            var adminClient = _factory.CreateClient();
            var adminUsername = TestHelpers.UniqueUsername("expiryadmin");
            var adminAuth = await TestHelpers.RegisterAndLoginAsync(adminClient, adminUsername);
            await SetRoleAsync(adminAuth.UserId, Roles.Admin);

            var adminLoginResp = await TestHelpers.LoginRawAsync(adminClient, adminUsername, adminAuth.Password);
            var adminBody = await adminLoginResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.Equal(4, adminBody.GetProperty("expiresInHours").GetInt32());
        }
    }
}
