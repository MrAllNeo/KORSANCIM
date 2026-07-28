using System.Net;
using System.Net.Http.Json;
using ForumApi.Data;
using Microsoft.Extensions.DependencyInjection;

namespace ForumApi.Tests
{
    public class AccountControllerTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public AccountControllerTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task ChangeUsername_Succeeds_With_Correct_Password_And_Returns_New_Token()
        {
            var client = _factory.CreateClient();
            var auth = await TestHelpers.RegisterAndLoginAsync(client, TestHelpers.UniqueUsername("renameme"));
            client.WithToken(auth.Token);

            var newUsername = TestHelpers.UniqueUsername("renamed");
            var resp = await client.PutAsJsonAsync("/api/account/username", new { NewUsername = newUsername, CurrentPassword = auth.Password });

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var body = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.Equal(newUsername, body.GetProperty("username").GetString());
            Assert.False(string.IsNullOrEmpty(body.GetProperty("token").GetString()));

            // Eski kullanıcı adıyla giriş artık başarısız olmalı, yeniyle başarılı olmalı.
            var oldLogin = await TestHelpers.LoginRawAsync(client, auth.Username, auth.Password);
            Assert.Equal(HttpStatusCode.BadRequest, oldLogin.StatusCode);

            var newLogin = await TestHelpers.LoginRawAsync(client, newUsername, auth.Password);
            Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        }

        [Fact]
        public async Task ChangeUsername_Fails_With_Wrong_Password()
        {
            var client = _factory.CreateClient();
            var auth = await TestHelpers.RegisterAndLoginAsync(client, TestHelpers.UniqueUsername("renamebad"));
            client.WithToken(auth.Token);

            var resp = await client.PutAsJsonAsync("/api/account/username",
                new { NewUsername = TestHelpers.UniqueUsername("shouldnotwork"), CurrentPassword = "YanlisSifre123!" });

            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

        [Fact]
        public async Task ChangeUsername_Rejects_Duplicate()
        {
            var client = _factory.CreateClient();
            var taken = TestHelpers.UniqueUsername("taken");
            await TestHelpers.RegisterAndLoginAsync(client, taken);

            var auth = await TestHelpers.RegisterAndLoginAsync(client, TestHelpers.UniqueUsername("wantstaken"));
            client.WithToken(auth.Token);

            var resp = await client.PutAsJsonAsync("/api/account/username", new { NewUsername = taken, CurrentPassword = auth.Password });
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        [Fact]
        public async Task ChangeEmail_Succeeds_And_Requires_Reverification()
        {
            var client = _factory.CreateClient();
            var auth = await TestHelpers.RegisterAndLoginAsync(client, TestHelpers.UniqueUsername("emailchange"));
            client.WithToken(auth.Token);

            var newEmail = $"new_{Guid.NewGuid():N}@test.local";
            var resp = await client.PutAsJsonAsync("/api/account/email", new { NewEmail = newEmail, CurrentPassword = auth.Password });

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var body = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.Equal(newEmail, body.GetProperty("email").GetString());

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FindAsync(auth.UserId);
            Assert.Equal(newEmail, user!.Email);
            // Testing ortamında bile e-posta değişikliği yeniden doğrulama gerektirir
            // (yalnızca KAYIT anındaki otomatik doğrulama testler için atlanıyor).
            Assert.False(user.IsEmailVerified);
        }

        [Fact]
        public async Task ChangePassword_Succeeds_And_Old_Password_Stops_Working()
        {
            var client = _factory.CreateClient();
            var auth = await TestHelpers.RegisterAndLoginAsync(client, TestHelpers.UniqueUsername("pwchange"));
            client.WithToken(auth.Token);

            var resp = await client.PutAsJsonAsync("/api/account/password",
                new { CurrentPassword = auth.Password, NewPassword = "YeniSifre456!" });
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

            var oldLogin = await TestHelpers.LoginRawAsync(client, auth.Username, auth.Password);
            Assert.Equal(HttpStatusCode.BadRequest, oldLogin.StatusCode);

            var newLogin = await TestHelpers.LoginRawAsync(client, auth.Username, "YeniSifre456!");
            Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        }

        [Fact]
        public async Task ChangePassword_Fails_With_Wrong_Current_Password()
        {
            var client = _factory.CreateClient();
            var auth = await TestHelpers.RegisterAndLoginAsync(client, TestHelpers.UniqueUsername("pwbad"));
            client.WithToken(auth.Token);

            var resp = await client.PutAsJsonAsync("/api/account/password",
                new { CurrentPassword = "YanlisSifre123!", NewPassword = "YeniSifre456!" });
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

        [Fact]
        public async Task UpdatePrivacy_Hides_Activity_From_Others_But_Not_Self()
        {
            var client = _factory.CreateClient();
            var auth = await TestHelpers.RegisterAndLoginAsync(client, TestHelpers.UniqueUsername("privacyuser"));
            client.WithToken(auth.Token);
            await TestHelpers.CreateTopicAsync(client, "Gizli olacak konu");

            var privacyResp = await client.PutAsJsonAsync("/api/account/privacy", new { ShowActivity = false });
            Assert.Equal(HttpStatusCode.OK, privacyResp.StatusCode);

            // Kendisi görür.
            var selfView = await client.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/users/profile/{auth.Username}");
            Assert.True(selfView.GetProperty("topics").GetArrayLength() > 0);

            // Başkası (anonim istek) görmez.
            var anonClient = _factory.CreateClient();
            var otherView = await anonClient.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/users/profile/{auth.Username}");
            Assert.Equal(0, otherView.GetProperty("topics").GetArrayLength());
            Assert.True(otherView.GetProperty("activityHidden").GetBoolean());
        }

        [Fact]
        public async Task DeleteAccount_Requires_Correct_Password()
        {
            var client = _factory.CreateClient();
            var auth = await TestHelpers.RegisterAndLoginAsync(client, TestHelpers.UniqueUsername("delbad"));
            client.WithToken(auth.Token);

            var req = new HttpRequestMessage(HttpMethod.Delete, "/api/account")
            {
                Content = JsonContent.Create(new { CurrentPassword = "YanlisSifre123!" })
            };
            var resp = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

        [Fact]
        public async Task DeleteAccount_Removes_User_And_Their_Topics()
        {
            var client = _factory.CreateClient();
            var auth = await TestHelpers.RegisterAndLoginAsync(client, TestHelpers.UniqueUsername("deluser"));
            client.WithToken(auth.Token);
            var topicId = await TestHelpers.CreateTopicAsync(client, "Silinecek kullanıcının konusu");

            var req = new HttpRequestMessage(HttpMethod.Delete, "/api/account")
            {
                Content = JsonContent.Create(new { CurrentPassword = auth.Password })
            };
            var resp = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Null(await db.Users.FindAsync(auth.UserId));
            Assert.Null(await db.Topics.FindAsync(topicId));
        }
    }
}
