// src/Controllers/CourseController.cs
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mini_LMS.Models;
using Mini_LMS.Services;

namespace Mini_LMS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CourseController : ControllerBase
    {
        private readonly MiniLMSContext _db;
        private readonly EmailService _email;
        private readonly ILogger<CourseController> _logger;

        public CourseController(
            MiniLMSContext db,
            EmailService email,
            ILogger<CourseController> logger)
        {
            _db = db;
            _email = email;
            _logger = logger;
        }

       

        public class CourseCreateDTO
        {
            public string Name { get; set; } = null!;
            public string? Type { get; set; }
            public int? Duration { get; set; }
            public string? Visibility { get; set; }
        }

        public class TakedownRequestDTO
        {
            public int CourseId { get; set; }
            public string Reason { get; set; } = null!;
        }

       

        [Authorize(Roles = "Trainer")]
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CourseCreateDTO dto)
        {
            // 1️⃣ Get trainer email from JWT
            var trainerEmail = User.FindFirst(ClaimTypes.Email)?.Value
                             ?? User.FindFirst("email")?.Value;
            if (string.IsNullOrEmpty(trainerEmail))
                return Unauthorized(new { message = "Trainer not authorized." });

            // 2️⃣ Load trainer user record
            var trainer = await _db.Users
                .SingleOrDefaultAsync(u => u.Email == trainerEmail && u.Role == "Trainer");
            if (trainer == null)
                return Unauthorized(new { message = "Trainer not found." });

            // 3️⃣ Create the course
            var course = new Course
            {
                TrainerId = trainer.Id,
                Name = dto.Name,
                Type = dto.Type,
                Duration = dto.Duration,
                Visibility = dto.Visibility ?? "Public",
                IsApproved = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Courses.Add(course);
            await _db.SaveChangesAsync();

            
            var learners = await _db.Users
                .Where(u => u.Role == "Learner" && u.IsActive == true)
                .ToListAsync();

            foreach (var learner in learners)
            {
               
                _db.Notifications.Add(new Notification
                {
                    UserId = learner.Id,
                    Type = "CourseCreated",
                    Message = $"New course '{course.Name}' is now available.",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });

                try
                {
                    await _email.SendNewCourseAvailableEmailAsync(learner.Email, course.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send course-created email to {Email}", learner.Email);
                }
            }

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Could not save one or more course-created notifications; continuing.");
            }

            return Ok(course);
        }

        

        [Authorize]
        [HttpGet("all")]
        public async Task<IActionResult> GetAll()
        {
            var courses = await _db.Courses
                .Include(c => c.Trainer)
                .ToListAsync();
            return Ok(courses);
        }

     

        [Authorize(Roles = "Trainer")]
        [HttpPost("request-takedown")]
        public async Task<IActionResult> RequestTakedown([FromBody] TakedownRequestDTO dto)
        {
            //Get trainer info from JWT
            var trainerEmail = User.FindFirst(ClaimTypes.Email)?.Value
                             ?? User.FindFirst("email")?.Value;
            if (string.IsNullOrEmpty(trainerEmail))
                return Unauthorized(new { message = "Not authorized." });

            // Ensure course exists
            var course = await _db.Courses.FindAsync(dto.CourseId);
            if (course == null)
                return NotFound(new { message = "Course not found." });

            // Lookup the trainer's user record so we have a valid UserId FK
            var trainerUser = await _db.Users
                .SingleOrDefaultAsync(u => u.Email == trainerEmail && u.Role == "Trainer");
            if (trainerUser == null)
                return Unauthorized(new { message = "Trainer account not found." });

            // Always store a Notification with the trainer's own UserId
            _db.Notifications.Add(new Notification
            {
                UserId = trainerUser.Id,                        // ← use trainer.Id
                Type = "TakedownRequested",
                Message = $"Trainer '{trainerEmail}' requested takedown of '{course.Name}'. Reason: {dto.Reason}",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            // 5️⃣ Send the hard‐coded admin email as before
            const string adminEmail = "tarun.balaji@relevantz.com";
            await _email.SendTakedownRequestEmailAsync(
                adminEmail,
                course.Name,
                trainerEmail
            );

            return Ok(new { message = "Takedown request recorded and emailed." });
        }


       
        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var course = await _db.Courses
                .Include(c => c.Trainer)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (course == null) return NotFound();
            return Ok(course);
        }

        
       [Authorize(Roles = "Admin")]
[HttpDelete("{id}")]
public async Task<IActionResult> Delete(int id)
{
    //Load the course with related modules
    var course = await _db.Courses
        .Include(c => c.Modules)
        .FirstOrDefaultAsync(c => c.Id == id);

    if (course == null)
        return NotFound(new { message = "Course not found." });

    // Remove related modules
    if (course.Modules.Any())
        _db.Modules.RemoveRange(course.Modules);

    // Remove related feedbacks
    var feedbacks = await _db.Feedbacks
        .Where(f => f.CourseId == course.Id)
        .ToListAsync();
    if (feedbacks.Any())
        _db.Feedbacks.RemoveRange(feedbacks);

    // Remove related notifications (takedown requests)
    var notes = await _db.Notifications
        .Where(n => n.Type == "TakedownRequested" && n.Message.Contains($"'{course.Name}'"))
        .ToListAsync();
    if (notes.Any())
        _db.Notifications.RemoveRange(notes);

    // Remove the course itself
    _db.Courses.Remove(course);

    // Save all changes in one transaction
    await _db.SaveChangesAsync();

    return NoContent();
}


        [Authorize(Roles = "Trainer")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CourseCreateDTO dto)
        {
            // Trainer email from JWT
            var trainerEmail = User.FindFirst(ClaimTypes.Email)?.Value
                             ?? User.FindFirst("email")?.Value;
            if (string.IsNullOrEmpty(trainerEmail))
                return Unauthorized(new { message = "Not authorized." });

            // Load course and ensure it belongs to this trainer
            var course = await _db.Courses
                .Include(c => c.Trainer)
                .SingleOrDefaultAsync(c => c.Id == id && c.Trainer.Email == trainerEmail);
            if (course == null)
                return NotFound(new { message = "Course not found or not your own." });

            //Apply updates
            course.Name = dto.Name;
            course.Type = dto.Type;
            course.Duration = dto.Duration;
            course.Visibility = dto.Visibility ?? course.Visibility;
            course.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(course);
        }
    }
    
    
}
