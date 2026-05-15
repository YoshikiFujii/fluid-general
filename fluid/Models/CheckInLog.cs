using System;

namespace fluid_general.Models
{
    public class CheckInLog
    {
        public int Id { get; set; }
        
        public int EventConfigId { get; set; }
        public EventConfig EventConfig { get; set; } = null!;

        public int MemberId { get; set; }
        public Member Member { get; set; } = null!;

        // 状態 (例: "参加済み", "未参加", "欠席")
        public string Status { get; set; } = "未参加";
        
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
