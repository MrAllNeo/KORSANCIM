namespace ForumApi.Models
{
    // Rozetin API yanıtlarında tekrar eden küçük görünümü — Topics/Comments/
    // Search/Profile/Auth yanıtlarının hepsi bunu kullanır.
    public class BadgeSummary
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string ColorTheme { get; set; } = string.Empty;
        public bool Shine { get; set; }

        public static BadgeSummary? From(Badge? badge) => badge == null ? null : new BadgeSummary
        {
            Id = badge.Id,
            Name = badge.Name,
            Icon = badge.Icon,
            ColorTheme = badge.ColorTheme,
            Shine = badge.Shine
        };
    }
}
