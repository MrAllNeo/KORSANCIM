namespace ForumApi.Models
{
    public static class ReportTargetType
    {
        public const string Topic = "Topic";
        public const string Comment = "Comment";
        public const string User = "User";

        public static readonly string[] All = { Topic, Comment, User };

        public static bool IsValid(string type) => All.Contains(type);
    }
}
