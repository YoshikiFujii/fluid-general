using System;

namespace fluid_general.Models
{
    public class EventConfig
    {
        public int Id { get; set; }
        public string EventName { get; set; } = string.Empty;
        public DateTime EventDate { get; set; } = DateTime.Now;
        
        // 旧Eventクラスからの移行項目
        public int Participants { get; set; }
        public string Status { get; set; } = "予定";
        public string TouchSound { get; set; } = "JR";
        public bool SameStudentSetting { get; set; } = true;
        public string RosterName { get; set; } = string.Empty;
    }
}
