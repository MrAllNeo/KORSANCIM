namespace ForumApi.Models
{
    public static class ReportStatus
    {
        public const string Pending = "Pending";
        public const string Reviewing = "Reviewing";
        public const string Resolved = "Resolved";
        public const string Dismissed = "Dismissed";

        public static readonly string[] All = { Pending, Reviewing, Resolved, Dismissed };
        public static readonly string[] Terminal = { Resolved, Dismissed };

        public static bool IsValid(string status) => All.Contains(status);
        public static bool IsTerminal(string status) => Terminal.Contains(status);
    }
}
