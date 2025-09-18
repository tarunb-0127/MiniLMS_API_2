// src/Controllers/NotificationsController.cs
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mini_LMS.Models;

namespace Mini_LMS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController : ControllerBase
    {
        private readonly MiniLMSContext _db;

        public NotificationsController(MiniLMSContext db)
        {
            _db = db;
        }

        // GET api/notifications
        // (you can also secure this with [Authorize])
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _db.Notifications
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
            return Ok(list);
        }

        // GET api/notifications/takedowns
        [Authorize(Roles = "Admin")]
        [HttpGet("takedowns")]
        public async Task<IActionResult> GetTakedowns()
        {
            var list = await _db.Notifications
                .Where(n => n.Type == "TakedownRequested")
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new {
                  n.Id,
                  n.UserId,
                  n.Message,
                  n.CreatedAt
                })
                .ToListAsync();
            return Ok(list);
        }

        // GET api/notifications/takedowns/count
        [Authorize(Roles = "Admin")]
        [HttpGet("takedowns/count")]
        public async Task<IActionResult> GetTakedownCount()
        {
            var count = await _db.Notifications
                .CountAsync(n => n.Type == "TakedownRequested");
            return Ok(new { count });
        }

        // DELETE api/notifications/{id}
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNotification(int id)
        {
            var note = await _db.Notifications.FindAsync(id);
            if (note == null) return NotFound();
            _db.Notifications.Remove(note);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
