using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ForumApi.Services
{
    // Brevo (eski adıyla Sendinblue) transactional e-posta API'sini sarar.
    // Basit HTTP çağrısı — ayrı bir SMTP kütüphanesine gerek yok.
    // Brevo:ApiKey tanımlı değilse (geliştirme ortamı) sessizce atlar; e-posta
    // gönderimi kayıt akışını bloklamamalı, yalnızca gerçek gönderim olmaz.
    public class EmailService
    {
        private const string BrevoEndpoint = "https://api.brevo.com/v3/smtp/email";

        private readonly HttpClient _httpClient;
        private readonly string? _apiKey;
        private readonly string _senderEmail;
        private readonly string _senderName;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<EmailService> logger)
        {
            _httpClient = httpClientFactory.CreateClient(nameof(EmailService));
            _apiKey = config["Brevo:ApiKey"];
            _senderEmail = config["Brevo:SenderEmail"] ?? "noreply@korsancim.local";
            _senderName = config["Brevo:SenderName"] ?? "KORSANCIM";
            _logger = logger;
        }

        public async Task SendVerificationEmailAsync(string toEmail, string toUsername, string verificationLink)
        {
            var subject = "KORSANCIM — e-posta adresini doğrula";
            var html =
                $"<p>Merhaba {toUsername},</p>" +
                $"<p>KORSANCIM'a hoş geldin. Konu/yorum yazabilmek için e-posta adresini doğrulaman gerekiyor:</p>" +
                $"<p><a href=\"{verificationLink}\">{verificationLink}</a></p>" +
                $"<p>Bu bağlantı 24 saat geçerlidir. Bu kaydı sen yapmadıysan bu e-postayı yok sayabilirsin.</p>";

            await SendAsync(toEmail, toUsername, subject, html);
        }

        private async Task SendAsync(string toEmail, string toName, string subject, string htmlContent)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning("Brevo:ApiKey tanımlı değil — e-posta gönderilmedi (alıcı: {Email}).", toEmail);
                return;
            }

            var payload = new
            {
                sender = new { name = _senderName, email = _senderEmail },
                to = new[] { new { email = toEmail, name = toName } },
                subject,
                htmlContent
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, BrevoEndpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("api-key", _apiKey);

            try
            {
                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Brevo e-posta gönderimi başarısız ({Status}): {Body}", response.StatusCode, body);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Brevo e-posta gönderimi sırasında ağ hatası (alıcı: {Email}).", toEmail);
            }
        }
    }
}
