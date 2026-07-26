using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ForumApi.Tests
{
    public class OwnershipTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public OwnershipTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Owner_Can_Edit_And_Delete_Own_Topic()
        {
            var client = _factory.CreateClient();
            var auth = await TestHelpers.RegisterAndLoginAsync(client, TestHelpers.UniqueUsername("owner"));
            client.WithToken(auth.Token);

            var topicId = await TestHelpers.CreateTopicAsync(client);

            var editResp = await client.PutAsJsonAsync($"/api/topics/{topicId}",
                new { Title = "Güncellenmiş Başlık", CategoryId = 1, Content = "Güncellenmiş içerik." });
            Assert.Equal(HttpStatusCode.OK, editResp.StatusCode);

            var deleteResp = await client.DeleteAsync($"/api/topics/{topicId}");
            Assert.Equal(HttpStatusCode.OK, deleteResp.StatusCode);
        }

        [Fact]
        public async Task NonOwner_Cannot_Edit_Or_Delete_Topic()
        {
            var ownerClient = _factory.CreateClient();
            var ownerAuth = await TestHelpers.RegisterAndLoginAsync(ownerClient, TestHelpers.UniqueUsername("ownr2"));
            ownerClient.WithToken(ownerAuth.Token);
            var topicId = await TestHelpers.CreateTopicAsync(ownerClient);

            var otherClient = _factory.CreateClient();
            var otherAuth = await TestHelpers.RegisterAndLoginAsync(otherClient, TestHelpers.UniqueUsername("other"));
            otherClient.WithToken(otherAuth.Token);

            var editResp = await otherClient.PutAsJsonAsync($"/api/topics/{topicId}",
                new { Title = "Hile Başlık", CategoryId = 1, Content = "Hile içerik." });
            Assert.Equal(HttpStatusCode.Forbidden, editResp.StatusCode);

            var deleteResp = await otherClient.DeleteAsync($"/api/topics/{topicId}");
            Assert.Equal(HttpStatusCode.Forbidden, deleteResp.StatusCode);
        }

        [Fact]
        public async Task NonOwner_Cannot_Edit_Or_Delete_Comment()
        {
            var ownerClient = _factory.CreateClient();
            var ownerAuth = await TestHelpers.RegisterAndLoginAsync(ownerClient, TestHelpers.UniqueUsername("cowner"));
            ownerClient.WithToken(ownerAuth.Token);
            var topicId = await TestHelpers.CreateTopicAsync(ownerClient);

            var commentResp = await ownerClient.PostAsJsonAsync("/api/comments", new { TopicId = topicId, Content = "İlk yorum." });
            commentResp.EnsureSuccessStatusCode();
            var commentBody = await commentResp.Content.ReadFromJsonAsync<JsonElement>();
            var commentId = commentBody.GetProperty("id").GetInt32();

            var otherClient = _factory.CreateClient();
            var otherAuth = await TestHelpers.RegisterAndLoginAsync(otherClient, TestHelpers.UniqueUsername("cother"));
            otherClient.WithToken(otherAuth.Token);

            var editResp = await otherClient.PutAsJsonAsync($"/api/comments/{commentId}", new { Content = "Hile yorum." });
            Assert.Equal(HttpStatusCode.Forbidden, editResp.StatusCode);

            var deleteResp = await otherClient.DeleteAsync($"/api/comments/{commentId}");
            Assert.Equal(HttpStatusCode.Forbidden, deleteResp.StatusCode);
        }
    }
}
