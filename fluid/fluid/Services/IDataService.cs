using fluid_general.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace fluid_general.Services
{
    public interface IDataService
    {
        // イベント関連
        Task<List<EventConfig>> GetEventsAsync();
        Task<EventConfig?> GetEventAsync(int id);
        Task<EventConfig> CreateEventAsync(EventConfig eventConfig);
        Task UpdateEventAsync(EventConfig eventConfig);
        Task DeleteEventAsync(int id);

        // 名簿関連
        Task<List<Member>> GetMembersAsync();
        Task<List<Member>> GetMembersByRosterAsync(string rosterName);
        Task<Member?> GetMemberAsync(string studentNumber);
        Task<Member> CreateMemberAsync(Member member);
        Task UpdateMemberAsync(Member member);
        Task DeleteMemberAsync(string studentNumber);

        // チェックイン（認証）関連
        Task<CheckInLog?> CheckInAsync(string rosterName, int excelId, int eventId);
        Task UpdateCheckInStatusAsync(string rosterName, int excelId, int eventId, string status);
        Task<List<CheckInLog>> GetCheckInLogsAsync(int eventId);
        
        // 名簿構成（マッピング）関連
        Task<RosterConfig?> GetRosterConfigAsync(string rosterName);
        Task SaveRosterConfigAsync(RosterConfig config);
    }
}
