using System;

namespace ForumApi.Models
{
    // Yönetim panelindeki her mutasyon burada iz bırakır — "kim ne zaman ne
    // yaptı" sorusu için. Elle silinemez/düzenlenemez, yalnızca eklenir.
    // ActorUsername anlık görüntü olarak tutulur: aktör kullanıcı silinse
    // veya adı değişse bile kaydın kim tarafından yapıldığı okunabilir kalır.
    public class AuditLog
    {
        public int Id { get; set; }

        public int? ActorUserId { get; set; }
        public User? Actor { get; set; }
        public string ActorUsername { get; set; } = string.Empty;

        public string Action { get; set; } = string.Empty;
        public string? TargetType { get; set; }
        public int? TargetId { get; set; }
        public string? Details { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
