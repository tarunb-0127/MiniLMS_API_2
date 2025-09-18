using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mini_LMS.Models;
using Mini_LMS.Services;
using System.Security.Claims;

namespace Mini_LMS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ModuleController : ControllerBase
    {
        private readonly MiniLMSContext _db;
        private readonly EmailService _email;
        private readonly BlobService _blob;

        public ModuleController(MiniLMSContext db, EmailService email, BlobService blob)
        {
            _db = db;
            _email = email;
            _blob = blob;
        }

        // DTOs
        public class ModuleCreateDto
        {
            public int CourseId { get; set; }
            public string Title { get; set; } = null!;
            public string? Content { get; set; }
            public IFormFile? File { get; set; }
        }

        public class ModuleUpdateDto
        {
            public string Title { get; set; } = null!;
            public string? Content { get; set; }
            public IFormFile? File { get; set; }
        }

        // Helper: Get current trainer
        private async Task<User?> GetTrainerAsync()
        {
            var email = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            if (email == null) return null;
            return await _db.Users.SingleOrDefaultAsync(u => u.Email == email && u.Role == "Trainer");
        }

        // Create Module (Trainer only)
        [Authorize(Roles = "Trainer")]
        [HttpPost("create")]
        public async Task<IActionResult> CreateModule([FromForm] ModuleCreateDto dto)
        {
            var trainer = await GetTrainerAsync();
            if (trainer == null) return Unauthorized("Trainer not authorized");

            var course = await _db.Courses.FindAsync(dto.CourseId);
            if (course == null) return NotFound("Course not found");
            if (course.TrainerId != trainer.Id)
                return Unauthorized("You can only add modules to your own courses");

            string? fileUrl = null;
            if (dto.File != null)
            {
                fileUrl = await _blob.UploadAsync(dto.File);
            }

            var module = new Module
            {
                CourseId = dto.CourseId,
                Name = dto.Title,
                Description = dto.Content,
                FilePath = fileUrl,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Modules.Add(module);
            await _db.SaveChangesAsync();

            // Initialize progress for all enrolled learners
            var enrolledLearners = await _db.Enrollments
                .Where(e => e.CourseId == dto.CourseId)
                .Select(e => e.LearnerId)
                .ToListAsync();

            foreach (var learnerId in enrolledLearners)
            {
                _db.Moduleprogresses.Add(new Moduleprogress
                {
                    LearnerId = learnerId,
                    ModuleId = module.Id,
                    ProgressPercentage = 0,
                    IsCompleted = false,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            await _db.SaveChangesAsync();

            await NotifyTrainerAsync(trainer, $"Module '{module.Name}' added to course '{course.Name}'.");

            return CreatedAtAction(nameof(GetModule), new { id = module.Id }, module);
        }

        //Update Module
        [Authorize(Roles = "Trainer")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateModule(int id, [FromForm] ModuleUpdateDto dto)
        {
            var trainer = await GetTrainerAsync();
            if (trainer == null) return Unauthorized("Trainer not authorized");

            var module = await _db.Modules.FindAsync(id);
            if (module == null) return NotFound("Module not found");

            var course = await _db.Courses.FindAsync(module.CourseId);
            if (course == null || course.TrainerId != trainer.Id)
                return Unauthorized("You can only update modules in your own courses");

            module.Name = dto.Title;
            module.Description = dto.Content;

            if (dto.File != null)
            {
                module.FilePath = await _blob.UploadAsync(dto.File);
            }

            module.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Reset progress for all learners if the file/content changed
            var moduleProgresses = await _db.Moduleprogresses.Where(p => p.ModuleId == module.Id).ToListAsync();
            foreach (var mp in moduleProgresses)
            {
                mp.ProgressPercentage = 0;
                mp.IsCompleted = false;
                mp.UpdatedAt = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync();

            await NotifyTrainerAsync(trainer, $"Module '{module.Name}' updated in course '{course.Name}'.");

            return Ok(module);
        }

        // Delete Module
        [Authorize(Roles = "Trainer")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteModule(int id)
        {
            var trainer = await GetTrainerAsync();
            if (trainer == null) return Unauthorized("Trainer not authorized");

            var module = await _db.Modules.FindAsync(id);
            if (module == null) return NotFound("Module not found");

            var course = await _db.Courses.FindAsync(module.CourseId);
            if (course == null || course.TrainerId != trainer.Id)
                return Unauthorized("You can only delete modules in your own courses");

            //Delete all related progress entries
            var progresses = await _db.Moduleprogresses.Where(p => p.ModuleId == module.Id).ToListAsync();
            _db.Moduleprogresses.RemoveRange(progresses);

            _db.Modules.Remove(module);
            await _db.SaveChangesAsync();

            await NotifyTrainerAsync(trainer, $"Module '{module.Name}' deleted from course '{course.Name}'.");

            return NoContent();
        }

        // Get single module
        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetModule(int id)
        {
            var module = await _db.Modules
                .Include(m => m.Course)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (module == null) return NotFound();
            return Ok(module);
        }

        // Get all modules for a course with progress for current learner
        [Authorize]
        [HttpGet("course/{courseId}")]
        public async Task<IActionResult> GetModulesByCourse(int courseId, [FromHeader] int learnerId)
        {
            var modules = await _db.Modules
                .Where(m => m.CourseId == courseId)
                .ToListAsync();

            //Merge with progress
            var progresses = await _db.Moduleprogresses
                .Where(p => p.LearnerId == learnerId && modules.Select(m => m.Id).Contains(p.ModuleId))
                .ToListAsync();

            var result = modules.Select(m =>
            {
                var p = progresses.FirstOrDefault(x => x.ModuleId == m.Id);
                return new
                {
                    m.Id,
                    m.CourseId,
                    m.Name,
                    m.Description,
                    m.FilePath,
                    ProgressPercentage = p?.ProgressPercentage ?? 0,
                    IsCompleted = p?.IsCompleted ?? false
                };
            });

            return Ok(result);
        }

        // Helper: notify trainer via notification + email
        private async Task NotifyTrainerAsync(User trainer, string message)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = trainer.Id,
                Type = "ModuleUpdate",
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            await _email.SendCourseUpdateEmailAsync(trainer.Email, message);
            await _db.SaveChangesAsync();
        }
    }
}
