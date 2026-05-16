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
            CheckInLog? log = null;
            Member? member = null;

            if (!string.IsNullOrEmpty(request.RosterName) && request.ExcelId > 0)
            {
                log = await _context.CheckInLogs
                    .FirstOrDefaultAsync(l => l.RosterName == request.RosterName && l.ExcelId == request.ExcelId && l.EventConfigId == request.EventId);
                member = await _context.Members.FirstOrDefaultAsync(m => m.RosterName == request.RosterName && m.ExcelId == request.ExcelId);
            }
            else if (!string.IsNullOrEmpty(request.StudentNumber))
            {
                member = await _context.Members.FirstOrDefaultAsync(m => m.StudentNumber == request.StudentNumber);
                if (member != null)
                {
                    log = await _context.CheckInLogs
                        .FirstOrDefaultAsync(l => l.RosterName == member.RosterName && l.ExcelId == member.ExcelId && l.EventConfigId == request.EventId);
                }
            }

            if (member == null && log == null) return NotFound(new { Message = "指定されたメンバーが見つかりませんでした。" });

            if (log == null)
            {
                log = new CheckInLog 
                { 
                    RosterName = member!.RosterName,
                    ExcelId = member.ExcelId,
                    EventConfigId = request.EventId, 
                    Status = "参加済み",
                    UpdatedAt = DateTime.Now
                };
                _context.CheckInLogs.Add(log);
            }
            else
            {
                if (log.Status == "参加済み") return BadRequest(new { Message = "既に参加済みです。" });
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
            CheckInLog? log = null;
            Member? member = null;

            if (!string.IsNullOrEmpty(request.RosterName) && request.ExcelId > 0)
            {
                log = await _context.CheckInLogs
                    .FirstOrDefaultAsync(l => l.RosterName == request.RosterName && l.ExcelId == request.ExcelId && l.EventConfigId == request.EventId);
            }
            else if (!string.IsNullOrEmpty(request.StudentNumber))
            {
                member = await _context.Members.FirstOrDefaultAsync(m => m.StudentNumber == request.StudentNumber);
                if (member != null)
                {
                    log = await _context.CheckInLogs
                        .FirstOrDefaultAsync(l => l.RosterName == member.RosterName && l.ExcelId == member.ExcelId && l.EventConfigId == request.EventId);
                }
            }

            if (log == null)
            {
                if (member == null && !string.IsNullOrEmpty(request.RosterName) && request.ExcelId > 0)
                {
                    // メンバーが存在しなくてもログだけ作成可能にする（復元性のため）
                    log = new CheckInLog 
                    { 
                        RosterName = request.RosterName,
                        ExcelId = request.ExcelId,
                        EventConfigId = request.EventId, 
                        Status = request.Status,
                        UpdatedAt = DateTime.Now
                    };
                }
                else if (member != null)
                {
                    log = new CheckInLog 
                    { 
                        RosterName = member.RosterName,
                        ExcelId = member.ExcelId,
                        EventConfigId = request.EventId, 
                        Status = request.Status,
                        UpdatedAt = DateTime.Now
                    };
                }
                else return NotFound(new { Message = "対象を特定できませんでした。" });

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

        [HttpPost]
        public async Task<ActionResult<Member>> PostMember(Member member)
        {
            // 名簿名とエクセル内IDで既存データを検索
            var existing = await _context.Members
                .FirstOrDefaultAsync(m => m.RosterName == member.RosterName && m.ExcelId == member.ExcelId);

            if (existing == null)
            {
                _context.Members.Add(member);
            }
            else
            {
                // 既存のデータを更新
                _context.Entry(existing).CurrentValues.SetValues(member);
                _context.Members.Update(existing);
            }

            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetMember), new { studentNumber = member.StudentNumber }, member);
        }

        [HttpPut("{rosterName}/{excelId}")]
        public async Task<IActionResult> PutMember(string rosterName, int excelId, Member member)
        {
            if (rosterName != member.RosterName || excelId != member.ExcelId) return BadRequest();
            _context.Entry(member).State = EntityState.Modified;
            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Members.Any(e => e.RosterName == rosterName && e.ExcelId == excelId)) return NotFound();
                else throw;
            }
            return NoContent();
        }

        [HttpDelete("{studentNumber}")]
        public async Task<IActionResult> DeleteMember(string studentNumber)
        {
            var member = await _context.Members.FirstOrDefaultAsync(m => m.StudentNumber == studentNumber);
            if (member == null) return NotFound();
            _context.Members.Remove(member);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }

    public class CheckInRequest
    {
        public string StudentNumber { get; set; } = string.Empty;
        public string RosterName { get; set; } = string.Empty;
        public int ExcelId { get; set; }
        public int EventId { get; set; }
    }

    public class StatusRequest
    {
        public string StudentNumber { get; set; } = string.Empty;
        public string RosterName { get; set; } = string.Empty;
        public int ExcelId { get; set; }
        public int EventId { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
