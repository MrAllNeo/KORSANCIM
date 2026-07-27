using System;

namespace ForumApi.Models
{
    // TargetType/TargetId polymorphic bir hedefi (Topic/Comment/User) işaret
    // eder; hedef üç ayrı tablodan birine ait olabildiği için gerçek bir
    // foreign key kurulamıyor — geçerlilik ReportsController'da elle
    // doğrulanıyor.
    public class Report
    {
        public int Id { get; set; }
        public string TargetType { get; set; } = string.Empty;
        public int TargetId { get; set; }

        public int ReporterUserId { get; set; }
        public User? Reporter { get; set; }

        public string Reason { get; set; } = string.Empty;
        public string? Note { get; set; }

        public string Status { get; set; } = ReportStatus.Pending;
        public string? ResolutionNote { get; set; }

        public int? HandledByUserId { get; set; }
        public User? HandledBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }
    }
}
