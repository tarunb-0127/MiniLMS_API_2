using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mini_LMS.Models;

namespace Mini_LMS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProgressController : ControllerBase
    {
        private readonly MiniLMSContext _context;

        public ProgressController(MiniLMSContext context)
        {
            _context = context;
        }

        // GET: api/Progress/course/4
        [HttpGet("course/{courseId}")]
        public async Task<IActionResult> GetCourseProgress(int courseId, [FromHeader] int learnerId)
        {
            if (learnerId <= 0) return BadRequest("Invalid learner ID");

            try
            {
                // Sum all module progresses for this course
                var progressList = await _context.Moduleprogresses
                    .Include(p => p.Module)
                    .Where(p => p.LearnerId == learnerId && p.Module.CourseId == courseId)
                    .ToListAsync();

                if (!progressList.Any()) return Ok(new { Progress = 0 });

                // Average of all module progresses
                var avgProgress = progressList.Average(p => (double?)p.ProgressPercentage ?? 0);
                return Ok(new { Progress = avgProgress });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return StatusCode(500, "Error fetching course progress");
            }
        }

        // GET: api/Progress/modules/4
        [HttpGet("modules/{courseId}")]
        public async Task<IActionResult> GetModulesProgress(int courseId, [FromHeader] int learnerId)
        {
            if (learnerId <= 0) return BadRequest("Invalid learner ID");

            try
            {
                var progressList = await _context.Moduleprogresses
                    .Include(p => p.Module)
                    .Where(p => p.LearnerId == learnerId && p.Module.CourseId == courseId)
                    .Select(p => new
                    {
                        ModuleId = p.ModuleId,
                        ProgressPercentage = p.ProgressPercentage ?? 0,
                        IsCompleted = p.IsCompleted ?? false
                    })
                    .ToListAsync();

                return Ok(progressList);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return StatusCode(500, "Error fetching modules progress");
            }
        }

        // POST: api/Progress/update
[HttpPost("update")]
public async Task<IActionResult> UpdateModuleProgress([FromBody] ProgressDto dto)
{
    if (dto.LearnerId <= 0 || dto.ModuleId <= 0 || dto.CourseId <= 0)
        return BadRequest("Invalid data: LearnerId, ModuleId, or CourseId is missing");

    try
    {
        // Fetch the module
        var module = await _context.Modules.FindAsync(dto.ModuleId);
        if (module == null)
            return BadRequest("Module not found");

        // Ensure CourseId is correct
        var courseIdToUse = dto.CourseId > 0 ? dto.CourseId : module.CourseId;

        // Check if progress already exists
        var existing = await _context.Moduleprogresses
            .FirstOrDefaultAsync(p => p.LearnerId == dto.LearnerId && p.ModuleId == dto.ModuleId);

        if (existing == null)
        {
            existing = new Moduleprogress
            {
                LearnerId = dto.LearnerId,
                ModuleId = dto.ModuleId,
                CourseId = courseIdToUse,
                ProgressPercentage = dto.ProgressPercentage,
                IsCompleted = dto.IsCompleted,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Moduleprogresses.Add(existing);
            Console.WriteLine($"Creating new progress: Learner {dto.LearnerId}, Module {dto.ModuleId}, Course {courseIdToUse}, Progress {dto.ProgressPercentage}%");
        }
        else
        {
            existing.ProgressPercentage = dto.ProgressPercentage;
            existing.IsCompleted = dto.IsCompleted;
            existing.CourseId = courseIdToUse; // Make sure courseId is stored correctly
            existing.UpdatedAt = DateTime.UtcNow;
            _context.Moduleprogresses.Update(existing);
            Console.WriteLine($"Updating progress: Learner {dto.LearnerId}, Module {dto.ModuleId}, Course {courseIdToUse}, Progress {dto.ProgressPercentage}%");
        }

        await _context.SaveChangesAsync();
        return Ok(existing);
    }
    catch (Exception ex)
    {
        Console.WriteLine("Error updating progress: " + ex);
        return StatusCode(500, "Error updating progress");
    }
}


       

        // POST: api/Progress/complete
        [HttpPost("complete")]
        public async Task<IActionResult> CompleteModule([FromBody] ProgressDto dto)
        {
            if (dto.LearnerId <= 0 || dto.ModuleId <= 0)
                return BadRequest("Invalid data");

            try
            {
                var module = await _context.Modules.FindAsync(dto.ModuleId);
                if (module == null) return BadRequest("Module not found");

                var existing = await _context.Moduleprogresses
                    .FirstOrDefaultAsync(p => p.LearnerId == dto.LearnerId && p.ModuleId == dto.ModuleId);

                if (existing == null)
                {
                    existing = new Moduleprogress
                    {
                        LearnerId = dto.LearnerId,
                        ModuleId = dto.ModuleId,
                        CourseId = module.CourseId,
                        ProgressPercentage = 100,
                        IsCompleted = true,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Moduleprogresses.Add(existing);
                }
                else
                {
                    existing.ProgressPercentage = 100;
                    existing.IsCompleted = true;
                    existing.UpdatedAt = DateTime.UtcNow;
                    _context.Moduleprogresses.Update(existing);
                }

                await _context.SaveChangesAsync();
                return Ok(existing);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return StatusCode(500, "Error completing module");
            }
        }
    }

    public class ProgressDto
    {
        public int LearnerId { get; set; }
        public int ModuleId { get; set; }

          public int CourseId { get; set; }  
        public int ProgressPercentage { get; set; }
        public bool IsCompleted { get; set; }
    }
}
