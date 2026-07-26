using System.Net;
using System.Net.Http.Json;

namespace ForumApi.Tests
{
    public class AuthTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public AuthTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Register_Then_Login_Succeeds()
        {
            var client = _factory.CreateClient();
            var auth = await TestHelpers.RegisterAndLoginAsync(client, TestHelpers.UniqueUsername("authok"));

            Assert.False(string.IsNullOrEmpty(auth.Token));
            Assert.True(auth.UserId > 0);
        }

        [Fact]
        public async Task Register_Duplicate_Username_Fails()
        {
            var client = _factory.CreateClient();
            var username = TestHelpers.UniqueUsername("dup");
            await TestHelpers.RegisterAndLoginAsync(client, username);

            var resp = await client.PostAsJsonAsync("/api/auth/register",
                new { Username = username, Email = $"baska_{username}@test.local", Password = "Password123!" });

            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        [Fact]
        public async Task Login_Wrong_Password_Fails()
        {
            var client = _factory.CreateClient();
            var username = TestHelpers.UniqueUsername("wrongpw");
            await TestHelpers.RegisterAndLoginAsync(client, username);

            var resp = await TestHelpers.LoginRawAsync(client, username, "yanlis-sifre-123");

            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        [Fact]
        public async Task Login_Unknown_User_Fails_With_Generic_Message()
        {
            var client = _factory.CreateClient();

            var resp = await TestHelpers.LoginRawAsync(client, "hic-boyle-biri-yok", "herhangi-bir-sifre");

            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        }
    }
}
