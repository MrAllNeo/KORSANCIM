using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ForumApi.Data;
using ForumApi.Models;
using Microsoft.Extensions.DependencyInjection;

namespace ForumApi.Tests
{
    public class ReportTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public ReportTests(CustomWebApplicationFactory factory)
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

        [Fact]
        public async Task User_Can_Report_A_Topic_And_See_It_In_Mine()
        {
            var authorClient = _factory.CreateClient();
            var authorAuth = await TestHelpers.RegisterAndLoginAsync(authorClient, TestHelpers.UniqueUsername("rtauthor"));
            authorClient.WithToken(authorAuth.Token);
            var topicId = await TestHelpers.CreateTopicAsync(authorClient);

            var reporterClient = _factory.CreateClient();
            var reporterAuth = await TestHelpers.RegisterAndLoginAsync(reporterClient, TestHelpers.UniqueUsername("rtreporter"));
            reporterClient.WithToken(reporterAuth.Token);

            var reportResp = await reporterClient.PostAsJsonAsync("/api/reports",
                new { TargetType = "Topic", TargetId = topicId, Reason = "Uygunsuz içerik" });
            Assert.Equal(HttpStatusCode.OK, reportResp.StatusCode);

            var mineResp = await reporterClient.GetAsync("/api/reports/mine");
            mineResp.EnsureSuccessStatusCode();
            var mine = await mineResp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(mine.GetArrayLength() > 0);
            Assert.Equal("Pending", mine[0].GetProperty("status").GetString());
        }

        [Fact]
        public async Task Reporting_Nonexistent_Target_Returns_NotFound()
        {
            var client = _factory.CreateClient();
            var auth = await TestHelpers.RegisterAndLoginAsync(client, TestHelpers.UniqueUsername("rtghost"));
            client.WithToken(auth.Token);

            var resp = await client.PostAsJsonAsync("/api/reports",
                new { TargetType = "Topic", TargetId = 999999, Reason = "Yok böyle bir konu" });

            Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        }

        [Fact]
        public async Task Duplicate_Pending_Report_From_Same_User_Is_Rejected()
        {
            var authorClient = _factory.CreateClient();
            var authorAuth = await TestHelpers.RegisterAndLoginAsync(authorClient, TestHelpers.UniqueUsername("rtauthor2"));
            authorClient.WithToken(authorAuth.Token);
            var topicId = await TestHelpers.CreateTopicAsync(authorClient);

            var reporterClient = _factory.CreateClient();
            var reporterAuth = await TestHelpers.RegisterAndLoginAsync(reporterClient, TestHelpers.UniqueUsername("rtdup"));
            reporterClient.WithToken(reporterAuth.Token);

            var first = await reporterClient.PostAsJsonAsync("/api/reports",
                new { TargetType = "Topic", TargetId = topicId, Reason = "İlk şikayet" });
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);

            var second = await reporterClient.PostAsJsonAsync("/api/reports",
                new { TargetType = "Topic", TargetId = topicId, Reason = "Aynı konu tekrar" });
            Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        }

        [Fact]
        public async Task Moderator_Can_List_And_Resolve_Reports()
        {
            var authorClient = _factory.CreateClient();
            var authorAuth = await TestHelpers.RegisterAndLoginAsync(authorClient, TestHelpers.UniqueUsername("rtauthor3"));
            authorClient.WithToken(authorAuth.Token);
            var topicId = await TestHelpers.CreateTopicAsync(authorClient, title: "Şikayet edilecek konu başlığı");

            var reporterClient = _factory.CreateClient();
            var reporterAuth = await TestHelpers.RegisterAndLoginAsync(reporterClient, TestHelpers.UniqueUsername("rtreporter2"));
            reporterClient.WithToken(reporterAuth.Token);

            var reportResp = await reporterClient.PostAsJsonAsync("/api/reports",
                new { TargetType = "Topic", TargetId = topicId, Reason = "Spam" });
            reportResp.EnsureSuccessStatusCode();
            var reportBody = await reportResp.Content.ReadFromJsonAsync<JsonElement>();
            var reportId = reportBody.GetProperty("reportId").GetInt32();

            var modClient = _factory.CreateClient();
            var modAuth = await TestHelpers.RegisterAndLoginAsync(modClient, TestHelpers.UniqueUsername("rtmod"));
            await SetRoleAsync(modAuth.UserId, Roles.Moderator);
            modAuth = await TestHelpers.LoginAsync(modClient, modAuth.Username, modAuth.Password);
            modClient.WithToken(modAuth.Token);

            var listResp = await modClient.GetAsync("/api/admin/reports?status=Pending");
            listResp.EnsureSuccessStatusCode();
            var list = await listResp.Content.ReadFromJsonAsync<JsonElement>();
            var items = list.GetProperty("items");
            Assert.True(items.GetArrayLength() > 0);

            var found = items.EnumerateArray().Any(r => r.GetProperty("id").GetInt32() == reportId
                && r.GetProperty("targetPreview").GetString() == "Şikayet edilecek konu başlığı");
            Assert.True(found);

            var resolveResp = await modClient.PutAsJsonAsync($"/api/admin/reports/{reportId}/status",
                new { Status = "Resolved", ResolutionNote = "Konu incelendi, kural ihlali yok." });
            Assert.Equal(HttpStatusCode.OK, resolveResp.StatusCode);

            var mineResp = await reporterClient.GetAsync("/api/reports/mine");
            mineResp.EnsureSuccessStatusCode();
            var mine = await mineResp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Resolved", mine[0].GetProperty("status").GetString());
        }

        [Fact]
        public async Task Regular_User_Cannot_List_Reports()
        {
            var client = _factory.CreateClient();
            var auth = await TestHelpers.RegisterAndLoginAsync(client, TestHelpers.UniqueUsername("rtplain"));
            client.WithToken(auth.Token);

            var resp = await client.GetAsync("/api/admin/reports");

            Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        }
    }
}
