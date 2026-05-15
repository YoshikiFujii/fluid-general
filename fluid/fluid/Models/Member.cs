using System.Collections.Generic;

namespace fluid_general.Models
{
    public class Member
    {
        public int Id { get; set; }
        
        // 基本項目
        public string StudentNumber { get; set; } = string.Empty; // 学籍番号・社員番号など
        public string Name { get; set; } = string.Empty;
        public string Kana { get; set; } = string.Empty;
        public string RosterName { get; set; } = string.Empty;

        // 動的な自由項目 (JSONとしてSQLiteに保存される)
        // 例: { "RoomNumber": "101", "Department": "情報工学科", "Year": "1年" }
        public Dictionary<string, string> CustomFields { get; set; } = new Dictionary<string, string>();
    }
}
