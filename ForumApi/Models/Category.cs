using System.Collections.Generic;

namespace ForumApi.Models
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }

        // Arayüzde gösterim sırası ve ikon adı (lucide ikon anahtarı).
        public int DisplayOrder { get; set; }
        public string Icon { get; set; } = "hash";

        public List<Topic> Topics { get; set; } = new();
    }
}
