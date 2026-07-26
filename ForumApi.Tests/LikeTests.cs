using System.Net.Http.Json;
using System.Text.Json;
using ForumApi.Data;
using ForumApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ForumApi.Tests
{
    public class LikeTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public LikeTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Like_Then_Unlike_Toggles_And_Counts_Correctly()
        {
            var client = _factory.CreateClient();
            var auth = await TestHelpers.RegisterAndLoginAsync(client, TestHelpers.UniqueUsername("liker"));
            client.WithToken(auth.Token);
            var topicId = await TestHelpers.CreateTopicAsync(client);

            var likeResp = await client.PostAsync($"/api/topics/{topicId}/like", null);
            likeResp.EnsureSuccessStatusCode();
            var likeBody = await likeResp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(likeBody.GetProperty("isLiked").GetBoolean());
            Assert.Equal(1, likeBody.GetProperty("likes").GetInt32());

            var unlikeResp = await client.PostAsync($"/api/topics/{topicId}/like", null);
            unlikeResp.EnsureSuccessStatusCode();
            var unlikeBody = await unlikeResp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(unlikeBody.GetProperty("isLiked").GetBoolean());
            Assert.Equal(0, unlikeBody.GetProperty("likes").GetInt32());
        }

        [Fact]
        public async Task Unique_Index_Prevents_Duplicate_Like_Row_For_Same_User()
        {
            var client = _factory.CreateClient();
            var auth = await TestHelpers.RegisterAndLoginAsync(client, TestHelpers.UniqueUsername("dupliker"));
            client.WithToken(auth.Token);
            var topicId = await TestHelpers.CreateTopicAsync(client);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.TopicLikes.Add(new TopicLike { TopicId = topicId, UserId = auth.UserId });
            await db.SaveChangesAsync();

            db.TopicLikes.Add(new TopicLike { TopicId = topicId, UserId = auth.UserId });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }
}
