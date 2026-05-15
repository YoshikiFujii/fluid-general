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
            db.Members.Add(member);
            await db.SaveChangesAsync();
            return member;
        }

        public async Task UpdateMemberAsync(Member member)
        {
            using var db = GetContext();
            db.Members.Update(member);
            await db.SaveChangesAsync();
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

        public async Task<CheckInLog?> CheckInAsync(string studentNumber, int eventId)
        {
            return await SetCheckInStatusAsync(studentNumber, eventId, "参加済み");
        }

        public async Task UpdateCheckInStatusAsync(string studentNumber, int eventId, string status)
        {
            await SetCheckInStatusAsync(studentNumber, eventId, status);
        }

        private async Task<CheckInLog?> SetCheckInStatusAsync(string studentNumber, int eventId, string status)
        {
            using var db = GetContext();
            var member = await db.Members.FirstOrDefaultAsync(m => m.StudentNumber == studentNumber);
            if (member == null) return null;

            var log = await db.CheckInLogs.FirstOrDefaultAsync(l => l.MemberId == member.Id && l.EventConfigId == eventId);
            if (log == null)
            {
                log = new CheckInLog
                {
                    MemberId = member.Id,
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
            await db.SaveChangesAsync();
            return log;
        }

        public async Task<List<CheckInLog>> GetCheckInLogsAsync(int eventId)
        {
            using var db = GetContext();
            return await db.CheckInLogs
                .Include(l => l.Member)
                .Where(l => l.EventConfigId == eventId)
                .ToListAsync();
        }
    }
}
