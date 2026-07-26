using ForumApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ForumApi.Tests
{
    public class FileUploadValidationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public FileUploadValidationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private FileUploadService GetService()
        {
            using var scope = _factory.Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<FileUploadService>();
        }

        private static IFormFile MakeFile(string fileName, string contentType, int sizeBytes)
        {
            var stream = new MemoryStream(new byte[sizeBytes]);
            return new FormFile(stream, 0, sizeBytes, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
        }

        [Fact]
        public void Rejects_Disallowed_Extension()
        {
            var uploads = GetService();
            var ok = uploads.IsValid(MakeFile("virus.exe", "application/octet-stream", 100), out var error);

            Assert.False(ok);
            Assert.False(string.IsNullOrEmpty(error));
        }

        [Fact]
        public void Rejects_Svg_Despite_Image_Content_Type()
        {
            // .svg bilerek yasaklı: içine script gömülüp stored XSS'e dönüşebiliyor.
            var uploads = GetService();
            var ok = uploads.IsValid(MakeFile("evil.svg", "image/svg+xml", 100), out _);

            Assert.False(ok);
        }

        [Fact]
        public void Rejects_Oversized_File()
        {
            var uploads = GetService();
            var ok = uploads.IsValid(MakeFile("big.png", "image/png", (int)FileUploadService.MaxFileSizeBytes + 1), out var error);

            Assert.False(ok);
            Assert.Contains("büyük", error);
        }

        [Fact]
        public void Rejects_Extension_ContentType_Mismatch()
        {
            var uploads = GetService();
            var ok = uploads.IsValid(MakeFile("photo.png", "text/html", 1024), out _);

            Assert.False(ok);
        }

        [Fact]
        public void Rejects_Empty_File()
        {
            var uploads = GetService();
            var ok = uploads.IsValid(MakeFile("empty.png", "image/png", 0), out _);

            Assert.False(ok);
        }

        [Fact]
        public void Accepts_Valid_Png()
        {
            var uploads = GetService();
            var ok = uploads.IsValid(MakeFile("photo.png", "image/png", 1024), out var error);

            Assert.True(ok);
            Assert.True(string.IsNullOrEmpty(error));
        }
    }
}
