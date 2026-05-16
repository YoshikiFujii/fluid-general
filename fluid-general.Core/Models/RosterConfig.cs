using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace fluid_general.Models
{
    public class RosterConfig
    {
        [Key]
        public string RosterName { get; set; } = string.Empty;

        // JSONとして保存されるマッピングリスト
        public List<ColumnMapping> Mappings { get; set; } = new List<ColumnMapping>();

        // 表示対象のカスタムフィールドキー
        public List<string> DisplayColumns { get; set; } = new List<string>();
    }

    public class ColumnMapping
    {
        public string Label { get; set; } = string.Empty;
        public int ColumnIndex { get; set; } = 1;
        public bool IsFixed { get; set; } = false;
    }
}
