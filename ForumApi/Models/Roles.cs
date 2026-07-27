namespace ForumApi.Models
{
    // Rol string olarak saklanır (SQLite'ta TEXT) — enum-int migrasyonu
    // karmaşasından kaçınmak ve admin panelinde okunabilir kalması için.
    //
    // Hiyerarşi: User < Moderator < Admin < Owner.
    // Owner tekil ve elle/migration ile atanır (bkz. AddOwnerRoleAndReports
    // migration'ı) — hiçbir uçtan "Owner" rolü verilemez, bu yüzden
    // Assignable listesinde yer almaz.
    public static class Roles
    {
        public const string User = "User";
        public const string Moderator = "Moderator";
        public const string Admin = "Admin";
        public const string Owner = "Owner";

        public static readonly string[] All = { User, Moderator, Admin, Owner };
        public static readonly string[] Assignable = { User, Moderator, Admin };

        // [Authorize(Roles = ...)] için hazır kombinasyonlar.
        public const string ModAndAbove = Moderator + "," + Admin + "," + Owner;
        public const string AdminAndAbove = Admin + "," + Owner;

        public static bool IsValid(string role) => All.Contains(role);
        public static bool IsAssignable(string role) => Assignable.Contains(role);
    }
}
