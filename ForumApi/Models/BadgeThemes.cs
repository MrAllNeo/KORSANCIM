namespace ForumApi.Models
{
    // Rozetler için önceden tanımlı renk temaları. Admin'in serbest CSS/renk
    // girmesine izin vermiyoruz (stil tutarlılığı ve injection riski), bunun
    // yerine css/app.css içinde hazır tanımlı bir tema seçiyor.
    public static class BadgeThemes
    {
        public const string Gold = "gold";
        public const string Cyan = "cyan";
        public const string Purple = "purple";
        public const string Green = "green";
        public const string Red = "red";
        public const string Plain = "plain";

        public static readonly string[] All = { Gold, Cyan, Purple, Green, Red, Plain };

        public static bool IsValid(string theme) => All.Contains(theme);
    }
}
