using System.Net;
using System.Net.Http.Json;
using ForumApi.Data;
using Microsoft.Extensions.DependencyInjection;

namespace ForumApi.Tests
{
    public class EmailVerificationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public EmailVerificationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        // Test ortamında kayıt otomatik doğrulanmış sayılır (bkz.
        // AuthController.Register); doğrulanmamış durumu test etmek için
        // doğrudan veritabanına yazıyoruz — tıpkı diğer test dosyalarındaki
        // SetRoleAsync deseni gibi.
        private async Task SetUnverifiedAsync(int userId, string? token = null, DateTime? expiresAt = null)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FindAsync(userId);
            user!.IsEmailVerified = false;
            user.EmailVerificationToken = token;
            user.EmailVerificationTokenExpiresAt = expiresAt;
            await db.SaveChangesAsync();
        }

        [Fact]
        public async Task Unverified_User_Cannot_Create_Topic_Or_Comment()
        {
            var client = _factory.CreateClient();
            var auth = await TestHelpers.RegisterAndLoginAsync(client, TestHelpers.UniqueUsername("unverified"));
            client.WithToken(auth.Token);

            await SetUnverifiedAsync(auth.UserId);

            using var form = new MultipartFormDataContent
            {
                { new StringContent("Doğrulanmamış konu"), "Title" },
                { new StringContent("1"), "CategoryId" },
                { new StringContent("İçerik gövdesi burada."), "Content" },
                { new StringContent("true"), "IsLegalTermsAccepted" }
            };
            var topicResp = await client.PostAsync("/api/topics", form);
            Assert.Equal(HttpStatusCode.Forbidden, topicResp.StatusCode);

            var commentResp = await client.PostAsJsonAsync("/api/comments", new { TopicId = 1, Content = "Yorum denemesi" });
            Assert.Equal(HttpStatusCode.Forbidden, commentResp.StatusCode);
        }

        [Fact]
        public async Task VerifyEmail_With_Valid_Token_Unlocks_Posting()
        {
            var client = _factory.CreateClient();
            var auth = await TestHelpers.RegisterAndLoginAsync(client, TestHelpers.UniqueUsername("tobeverified"));
            client.WithToken(auth.Token);

            const string token = "test-token-valid-123";
            await SetUnverifiedAsync(auth.UserId, token, DateTime.UtcNow.AddHours(1));

            var verifyResp = await client.PostAsJsonAsync("/api/auth/verify-email", new { Token = token });
            Assert.Equal(HttpStatusCode.OK, verifyResp.StatusCode);

            var topicId = await TestHelpers.CreateTopicAsync(client);
            Assert.True(topicId > 0);
        }

        [Fact]
        public async Task VerifyEmail_Rejects_Expired_Or_Unknown_Token()
        {
            var client = _factory.CreateClient();
            var auth = await TestHelpers.RegisterAndLoginAsync(client, TestHelpers.UniqueUsername("expiredtoken"));

            const string expiredToken = "test-token-expired-456";
            await SetUnverifiedAsync(auth.UserId, expiredToken, DateTime.UtcNow.AddHours(-1));

            var expiredResp = await client.PostAsJsonAsync("/api/auth/verify-email", new { Token = expiredToken });
            Assert.Equal(HttpStatusCode.BadRequest, expiredResp.StatusCode);

            var unknownResp = await client.PostAsJsonAsync("/api/auth/verify-email", new { Token = "hic-boyle-bir-token-yok" });
            Assert.Equal(HttpStatusCode.BadRequest, unknownResp.StatusCode);
        }

        [Fact]
        public async Task ResendVerification_Only_Works_When_Unverified()
        {
            var client = _factory.CreateClient();
            var auth = await TestHelpers.RegisterAndLoginAsync(client, TestHelpers.UniqueUsername("resend"));
            client.WithToken(auth.Token);

            // Test ortamında zaten doğrulanmış — tekrar göndermeye gerek yok.
            var alreadyVerifiedResp = await client.PostAsync("/api/auth/resend-verification", null);
            Assert.Equal(HttpStatusCode.BadRequest, alreadyVerifiedResp.StatusCode);

            await SetUnverifiedAsync(auth.UserId);

            var resendResp = await client.PostAsync("/api/auth/resend-verification", null);
            Assert.Equal(HttpStatusCode.OK, resendResp.StatusCode);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FindAsync(auth.UserId);
            Assert.False(string.IsNullOrEmpty(user!.EmailVerificationToken));
        }
    }
}
