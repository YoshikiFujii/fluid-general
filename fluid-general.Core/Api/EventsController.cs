using fluid_general.Data;
using fluid_general.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace fluid_general.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class EventsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public EventsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/events
        [HttpGet]
        public async Task<ActionResult<IEnumerable<EventConfig>>> GetEvents()
        {
            return await _context.Events.ToListAsync();
        }

        // GET: api/events/5
        [HttpGet("{id}")]
        public async Task<ActionResult<EventConfig>> GetEvent(int id)
        {
            var ev = await _context.Events.FindAsync(id);

            if (ev == null)
            {
                return NotFound();
            }

            return ev;
        }

        // POST: api/events
        [HttpPost]
        public async Task<ActionResult<EventConfig>> CreateEvent(EventConfig eventConfig)
        {
            _context.Events.Add(eventConfig);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetEvent), new { id = eventConfig.Id }, eventConfig);
        }

        // PUT: api/events/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEvent(int id, EventConfig eventConfig)
        {
            if (id != eventConfig.Id)
            {
                return BadRequest();
            }

            _context.Entry(eventConfig).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EventExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // DELETE: api/events/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var ev = await _context.Events.FindAsync(id);
            if (ev == null)
            {
                return NotFound();
            }

            _context.Events.Remove(ev);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/events/5/logs
        [HttpGet("{id}/logs")]
        public async Task<ActionResult<IEnumerable<CheckInLog>>> GetCheckInLogs(int id)
        {
            return await _context.CheckInLogs
                .Include(l => l.Member)
                .Where(l => l.EventConfigId == id)
                .ToListAsync();
        }

        private bool EventExists(int id)
        {
            return _context.Events.Any(e => e.Id == id);
        }
    }
}
