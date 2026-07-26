namespace ForumApi.Models
{
    // Rol string olarak saklanır (SQLite'ta TEXT) — enum-int migrasyonu
    // karmaşasından kaçınmak ve admin panelinde okunabilir kalması için.
    public static class Roles
    {
        public const string User = "User";
        public const string Moderator = "Moderator";
        public const string Admin = "Admin";

        public static readonly string[] All = { User, Moderator, Admin };

        public static bool IsValid(string role) => All.Contains(role);
    }
}
