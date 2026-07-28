namespace ForumApi.Models
{
    // Denetim kaydına yazılan işlem adları — AdminController'daki her
    // mutasyon burada sabitlenir, elle string yazılmaz.
    public static class AuditActions
    {
        public const string BanUser = "BanUser";
        public const string UnbanUser = "UnbanUser";
        public const string ChangeRole = "ChangeRole";
        public const string DeleteTopic = "DeleteTopic";
        public const string EditTopic = "EditTopic";
        public const string DeleteComment = "DeleteComment";
        public const string EditComment = "EditComment";
        public const string UpdateReportStatus = "UpdateReportStatus";
        public const string CreateBadge = "CreateBadge";
        public const string UpdateBadge = "UpdateBadge";
        public const string DeleteBadge = "DeleteBadge";
        public const string AssignBadge = "AssignBadge";
        public const string CreateCategory = "CreateCategory";
        public const string UpdateCategory = "UpdateCategory";
        public const string DeleteCategory = "DeleteCategory";
        public const string UpdateSettings = "UpdateSettings";
    }
}
