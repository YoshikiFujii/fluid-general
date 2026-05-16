using fluid_general.Data;
using fluid_general.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace fluid_general.Services
{
    public class LocalDataService : IDataService
    {
        private AppDbContext GetContext() => new AppDbContext();

        public async Task<List<EventConfig>> GetEventsAsync()
        {
            using var db = GetContext();
            return await db.Events.ToListAsync();
        }

        public async Task<EventConfig?> GetEventAsync(int id)
        {
            using var db = GetContext();
            return await db.Events.FindAsync(id);
        }

        public async Task<EventConfig> CreateEventAsync(EventConfig eventConfig)
        {
            using var db = GetContext();
            db.Events.Add(eventConfig);
            await db.SaveChangesAsync();
            return eventConfig;
        }

        public async Task UpdateEventAsync(EventConfig eventConfig)
        {
            using var db = GetContext();
            db.Events.Update(eventConfig);
            await db.SaveChangesAsync();
        }

        public async Task DeleteEventAsync(int id)
        {
            using var db = GetContext();
            var ev = await db.Events.FindAsync(id);
            if (ev != null)
            {
                db.Events.Remove(ev);
                await db.SaveChangesAsync();
            }
        }

        public async Task<List<Member>> GetMembersAsync()
        {
            using var db = GetContext();
            return await db.Members.ToListAsync();
        }

        public async Task<List<Member>> GetMembersByRosterAsync(string rosterName)
        {
            using var db = GetContext();
            return await db.Members
                .Where(m => m.RosterName == rosterName)
                .ToListAsync();
        }

        public async Task<Member?> GetMemberAsync(string studentNumber)
        {
            using var db = GetContext();
            return await db.Members.FirstOrDefaultAsync(m => m.StudentNumber == studentNumber);
        }

        public async Task<Member> CreateMemberAsync(Member member)
        {
            using var db = GetContext();
            
            // 名簿名とエクセル内IDで既存データを検索
            var existing = await db.Members
                .FirstOrDefaultAsync(m => m.RosterName == member.RosterName && m.ExcelId == member.ExcelId);

            if (existing == null)
            {
                db.Members.Add(member);
            }
            else
            {
                // 既存のデータを更新 (Idは変更しない)
                existing.StudentNumber = member.StudentNumber;
                existing.Name = member.Name;
                existing.Kana = member.Kana;
                existing.CustomFields = member.CustomFields;
                db.Members.Update(existing);
            }
            
            await db.SaveChangesAsync();
            return member;
        }

        public async Task UpdateMemberAsync(Member member)
        {
            using var db = GetContext();
            var existing = await db.Members
                .FirstOrDefaultAsync(m => m.RosterName == member.RosterName && m.ExcelId == member.ExcelId);
            
            if (existing != null)
            {
                db.Entry(existing).CurrentValues.SetValues(member);
                await db.SaveChangesAsync();
            }
        }

        public async Task DeleteMemberAsync(string studentNumber)
        {
            using var db = GetContext();
            var member = await db.Members.FirstOrDefaultAsync(m => m.StudentNumber == studentNumber);
            if (member != null)
            {
                db.Members.Remove(member);
                await db.SaveChangesAsync();
            }
        }

        public async Task<CheckInLog?> CheckInAsync(string rosterName, int excelId, int eventId)
        {
            using var db = GetContext();
            var log = await db.CheckInLogs
                .FirstOrDefaultAsync(l => l.RosterName == rosterName && l.ExcelId == excelId && l.EventConfigId == eventId);
            
            if (log == null)
            {
                log = new CheckInLog
                {
                    RosterName = rosterName,
                    ExcelId = excelId,
                    EventConfigId = eventId,
                    Status = "参加済み",
                    UpdatedAt = DateTime.Now
                };
                db.CheckInLogs.Add(log);
            }
            else
            {
                log.Status = "参加済み";
                log.UpdatedAt = DateTime.Now;
            }

            try
            {
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException != null ? $"\nInner: {ex.InnerException.Message}" : "";
                App.LogError(new Exception($"CheckInAsync Error: {ex.Message}{inner}", ex));
                throw;
            }
            return log;
        }

        public async Task UpdateCheckInStatusAsync(string rosterName, int excelId, int eventId, string status)
        {
            using var db = GetContext();
            var log = await db.CheckInLogs
                .FirstOrDefaultAsync(l => l.RosterName == rosterName && l.ExcelId == excelId && l.EventConfigId == eventId);

            if (log == null)
            {
                log = new CheckInLog
                {
                    RosterName = rosterName,
                    ExcelId = excelId,
                    EventConfigId = eventId,
                    Status = status,
                    UpdatedAt = DateTime.Now
                };
                db.CheckInLogs.Add(log);
            }
            else
            {
                log.Status = status;
                log.UpdatedAt = DateTime.Now;
            }

            try
            {
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException != null ? $"\nInner: {ex.InnerException.Message}" : "";
                App.LogError(new Exception($"UpdateCheckInStatusAsync Error: {ex.Message}{inner}", ex));
                throw;
            }
        }

        public async Task<List<CheckInLog>> GetCheckInLogsAsync(int eventId)
        {
            using var db = GetContext();
            return await db.CheckInLogs
                .Include(l => l.Member)
                .Where(l => l.EventConfigId == eventId)
                .ToListAsync();
        }
        
        public async Task<RosterConfig?> GetRosterConfigAsync(string rosterName)
        {
            using var db = GetContext();
            return await db.RosterConfigs.FindAsync(rosterName);
        }

        public async Task SaveRosterConfigAsync(RosterConfig config)
        {
            using var db = GetContext();
            var existing = await db.RosterConfigs.FindAsync(config.RosterName);
            if (existing == null)
            {
                db.RosterConfigs.Add(config);
            }
            else
            {
                existing.Mappings = config.Mappings;
                db.RosterConfigs.Update(existing);
            }
            await db.SaveChangesAsync();
        }
    }
}
