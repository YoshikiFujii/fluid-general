using fluid_general.Data;
using fluid_general.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace fluid_general.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class MembersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MembersController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/members
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Member>>> GetMembers()
        {
            return await _context.Members.ToListAsync();
        }

        // GET: api/members/roster/Default
        [HttpGet("roster/{rosterName}")]
        public async Task<ActionResult<IEnumerable<Member>>> GetMembersByRoster(string rosterName)
        {
            return await _context.Members.Where(m => m.RosterName == rosterName).ToListAsync();
        }

        // GET: api/members/12345
        [HttpGet("{studentNumber}")]
        public async Task<ActionResult<Member>> GetMember(string studentNumber)
        {
            var member = await _context.Members.FirstOrDefaultAsync(m => m.StudentNumber == studentNumber);

            if (member == null)
            {
                return NotFound();
            }

            return member;
        }

        // POST: api/members/checkin
        [HttpPost("checkin")]
        public async Task<IActionResult> CheckIn([FromBody] CheckInRequest request)
        {
            var member = await _context.Members.FirstOrDefaultAsync(m => m.StudentNumber == request.StudentNumber);
            if (member == null) return NotFound(new { Message = "指定された学籍番号/IDは見つかりませんでした。" });

            // 実際の運用では EventId の存在チェックなども行います
            var log = await _context.CheckInLogs.FirstOrDefaultAsync(l => l.MemberId == member.Id && l.EventConfigId == request.EventId);
            
            if (log == null)
            {
                log = new CheckInLog 
                { 
                    MemberId = member.Id, 
                    EventConfigId = request.EventId, 
                    Status = "参加済み",
                    UpdatedAt = DateTime.Now
                };
                _context.CheckInLogs.Add(log);
            }
            else
            {
                // すでに参加済みの場合はエラーにするなどの処理を追加できます
                if (log.Status == "参加済み")
                {
                    return BadRequest(new { Message = "既に参加済みです。" });
                }

                log.Status = "参加済み";
                log.UpdatedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return Ok(new { Message = "チェックイン成功", Member = member, Log = log });
        }

        // POST: api/members/status
        [HttpPost("status")]
        public async Task<IActionResult> SetStatus([FromBody] StatusRequest request)
        {
            var member = await _context.Members.FirstOrDefaultAsync(m => m.StudentNumber == request.StudentNumber);
            if (member == null) return NotFound(new { Message = "メンバーが見つかりません。" });

            var log = await _context.CheckInLogs.FirstOrDefaultAsync(l => l.MemberId == member.Id && l.EventConfigId == request.EventId);
            
            if (log == null)
            {
                log = new CheckInLog 
                { 
                    MemberId = member.Id, 
                    EventConfigId = request.EventId, 
                    Status = request.Status,
                    UpdatedAt = DateTime.Now
                };
                _context.CheckInLogs.Add(log);
            }
            else
            {
                log.Status = request.Status;
                log.UpdatedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return Ok(new { Message = "ステータス更新成功", Log = log });
        }
    }

    public class CheckInRequest
    {
        public string StudentNumber { get; set; } = string.Empty;
        public int EventId { get; set; }
    }

    public class StatusRequest
    {
        public string StudentNumber { get; set; } = string.Empty;
        public int EventId { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
