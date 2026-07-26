using System.Net.Http.Json;
using System.Text.Json;

namespace ForumApi.Tests
{
    public class SearchTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public SearchTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Search_Finds_Topic_By_Title()
        {
            var client = _factory.CreateClient();
            var auth = await TestHelpers.RegisterAndLoginAsync(client, TestHelpers.UniqueUsername("srchr"));
            client.WithToken(auth.Token);

            var needle = Guid.NewGuid().ToString("N")[..12];
            await TestHelpers.CreateTopicAsync(client, title: $"Bu konu {needle} kelimesini içeriyor");

            var resp = await client.GetAsync($"/api/search?q={needle}");
            resp.EnsureSuccessStatusCode();

            var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(body.GetProperty("topics").GetArrayLength() > 0);
        }

        [Fact]
        public async Task Search_Finds_User_By_Username()
        {
            var client = _factory.CreateClient();
            var username = TestHelpers.UniqueUsername("findme");
            await TestHelpers.RegisterAndLoginAsync(client, username);

            var resp = await client.GetAsync($"/api/search?q={username}");
            resp.EnsureSuccessStatusCode();

            var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(body.GetProperty("users").GetArrayLength() > 0);
        }

        [Fact]
        public async Task Search_Finds_Comment_By_Content()
        {
            var client = _factory.CreateClient();
            var auth = await TestHelpers.RegisterAndLoginAsync(client, TestHelpers.UniqueUsername("csrchr"));
            client.WithToken(auth.Token);

            var topicId = await TestHelpers.CreateTopicAsync(client);
            var needle = Guid.NewGuid().ToString("N")[..12];

            var commentResp = await client.PostAsJsonAsync("/api/comments",
                new { TopicId = topicId, Content = $"Bu yorumda {needle} geçiyor." });
            commentResp.EnsureSuccessStatusCode();

            var resp = await client.GetAsync($"/api/search?q={needle}");
            resp.EnsureSuccessStatusCode();

            var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(body.GetProperty("comments").GetArrayLength() > 0);
        }
    }
}
