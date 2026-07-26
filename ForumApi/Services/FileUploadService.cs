namespace ForumApi.Services
{
    // Yüklenen dosyaları doğrular ve wwwroot/uploads altına güvenli adla yazar.
    public class FileUploadService
    {
        public const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
        public const int MaxFilesPerTopic = 5;

        // Sadece görsel. .svg bilerek dışarıda: içine script gömülebiliyor ve
        // aynı origin'den servis edildiği için stored XSS'e dönüşür.
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp"
        };

        private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/png", "image/gif", "image/webp"
        };

        private readonly IWebHostEnvironment _env;

        public FileUploadService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public static string AllowedDescription =>
            $"İzin verilen türler: {string.Join(", ", AllowedExtensions)} — en fazla {MaxFileSizeBytes / (1024 * 1024)} MB.";

        public bool IsValid(IFormFile file, out string error)
        {
            if (file.Length == 0)
            {
                error = "Boş dosya yüklenemez.";
                return false;
            }

            if (file.Length > MaxFileSizeBytes)
            {
                error = $"'{file.FileName}' çok büyük. {AllowedDescription}";
                return false;
            }

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            {
                error = $"'{file.FileName}' desteklenmeyen bir dosya türü. {AllowedDescription}";
                return false;
            }

            if (!AllowedContentTypes.Contains(file.ContentType))
            {
                error = $"'{file.FileName}' içerik türü ({file.ContentType}) uzantısıyla uyuşmuyor.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        // Dosyayı rastgele adla kaydeder; kullanıcının verdiği ad hiç kullanılmaz
        // (path traversal ve çift uzantı hilelerini tamamen ortadan kaldırır).
        public async Task<string> SaveAsync(IFormFile file, string prefix)
        {
            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadsFolder);

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{prefix}_{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(uploadsFolder, fileName);

            await using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);

            return "/uploads/" + fileName;
        }

        // Konu silinirken eklerini de temizler. Sessizce çalışır — dosya zaten
        // yoksa veya silinemezse istek başarısız sayılmaz.
        public void DeleteAll(IEnumerable<string> fileUrls)
        {
            var uploadsFolder = Path.GetFullPath(Path.Combine(_env.WebRootPath, "uploads"));

            foreach (var url in fileUrls)
            {
                if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("/uploads/", StringComparison.Ordinal))
                {
                    continue;
                }

                var candidate = Path.GetFullPath(Path.Combine(uploadsFolder, Path.GetFileName(url)));

                // Yol, uploads klasörünün dışına çıkmamalı (dizin aşımına karşı).
                if (!candidate.StartsWith(uploadsFolder + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {
                    if (File.Exists(candidate)) File.Delete(candidate);
                }
                catch (IOException)
                {
                    // Dosya kilitliyse silme atlanır; kayıtlar zaten temizlendi.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
