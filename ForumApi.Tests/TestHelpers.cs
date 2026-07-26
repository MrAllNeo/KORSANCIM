using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ForumApi.Tests
{
    public record AuthResult(string Token, int UserId, string Username, string Password);

    public static class TestHelpers
    {
        // Kullanıcı adı kuralları: 3-32 karakter, yalnızca harf/rakam/_.-
        // Testler arası çakışmayı önlemek için her çağrıda benzersiz üretilir.
        public static string UniqueUsername(string prefix)
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var name = $"{prefix}_{suffix}";
            return name.Length > 32 ? name[..32] : name;
        }

        public static HttpClient WithToken(this HttpClient client, string token)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        public static Task<HttpResponseMessage> LoginRawAsync(HttpClient client, string usernameOrEmail, string password) =>
            client.PostAsJsonAsync("/api/auth/login", new { UsernameOrEmail = usernameOrEmail, Password = password });

        public static async Task<AuthResult> LoginAsync(HttpClient client, string usernameOrEmail, string password)
        {
            var resp = await LoginRawAsync(client, usernameOrEmail, password);
            resp.EnsureSuccessStatusCode();

            var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
            return new AuthResult(
                body.GetProperty("token").GetString()!,
                body.GetProperty("userId").GetInt32(),
                usernameOrEmail,
                password);
        }

        public static async Task<AuthResult> RegisterAndLoginAsync(HttpClient client, string username, string password = "Password123!")
        {
            var email = $"{username.ToLower()}@test.local";
            var registerResp = await client.PostAsJsonAsync("/api/auth/register", new { Username = username, Email = email, Password = password });
            registerResp.EnsureSuccessStatusCode();

            return await LoginAsync(client, username, password);
        }

        // client'ın Authorization başlığı zaten konu sahibinin token'ı olmalı.
        public static async Task<int> CreateTopicAsync(HttpClient client, string title = "Test Konusu", int categoryId = 1, string content = "Test içerik gövdesi.")
        {
            using var form = new MultipartFormDataContent
            {
                { new StringContent(title), "Title" },
                { new StringContent(categoryId.ToString()), "CategoryId" },
                { new StringContent(content), "Content" },
                { new StringContent("true"), "IsLegalTermsAccepted" }
            };

            var resp = await client.PostAsync("/api/topics", form);
            resp.EnsureSuccessStatusCode();

            var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
            return body.GetProperty("id").GetInt32();
        }
    }
}
