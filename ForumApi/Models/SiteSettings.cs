namespace ForumApi.Models
{
    // Tek satırlık site geneli ayarlar (Id her zaman 1). Anahtar/değer
    // tablosu yerine sabit kolonlar seçildi — yalnızca 3 bilinen ayar var,
    // soyutlamaya gerek yok.
    public class SiteSettings
    {
        public int Id { get; set; } = 1;

        public bool RegistrationOpen { get; set; } = true;
        public bool MaintenanceMode { get; set; } = false;
        public string? AnnouncementText { get; set; }
    }
}
