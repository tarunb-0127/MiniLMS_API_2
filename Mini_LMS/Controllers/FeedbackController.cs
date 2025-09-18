using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mini_LMS.Models;
using Mini_LMS.Services;

namespace Mini_LMS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FeedbacksController : ControllerBase
    {
        private readonly MiniLMSContext _db;
        private readonly EmailService _email;

        public FeedbacksController(MiniLMSContext db, EmailService email)
        {
            _db = db;
            _email = email;
        }

        // Create course-level feedback
        [HttpPost]
        public async Task<IActionResult> Create([FromForm] FeedbackCreateRequest req)
        {
            if (!await _db.Users.AnyAsync(u => u.Id == req.LearnerId && u.Role == "Learner"))
                return BadRequest(new { message = "Invalid learnerId" });

            var course = await _db.Courses.FindAsync(req.CourseId);
            if (course == null)
                return BadRequest(new { message = "Invalid courseId" });

            var feedback = new Feedback
            {
                LearnerId = (int)req.LearnerId,
                CourseId = req.CourseId,
                Message = req.Message,
                Rating = req.Rating ?? 0,
                SubmittedAt = DateTime.UtcNow
            };

            _db.Feedbacks.Add(feedback);
            await _db.SaveChangesAsync();

            //Notify trainer
            var trainer = await _db.Users.FindAsync(course.TrainerId);
            if (trainer != null)
            {
                _db.Notifications.Add(new Notification
                {
                    UserId = trainer.Id,
                    Type = "FeedbackReceived",
                    Message = $"New feedback on '{course.Name}': \"{feedback.Message}\" (Rating: {feedback.Rating})",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();

                await _email.SendFeedbackNotificationEmailAsync(
                    trainer.Email,
                    learnerName: (await _db.Users.FindAsync(req.LearnerId))!.Email,
                    courseName: course.Name
                );
            }

            return CreatedAtAction(nameof(GetByCourse), new { courseId = req.CourseId }, feedback);
        }

        //Get all feedback for a course
        [HttpGet("course/{courseId}")]
        public async Task<IActionResult> GetByCourse(int courseId)
        {
            var list = await _db.Feedbacks
                .Where(f => f.CourseId == courseId)
                .Include(f => f.Learner)
                .ToListAsync();
            return Ok(list);
        }

        // Get single feedback by ID
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var feedback = await _db.Feedbacks
                .Include(f => f.Learner)
                .Include(f => f.Course)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (feedback == null) return NotFound();
            return Ok(feedback);
        }

        //Update feedback (message or rating)
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromForm] FeedbackUpdateRequest req)
        {
            var feedback = await _db.Feedbacks.FindAsync(id);
            if (feedback == null) return NotFound();

            feedback.Message = req.Message ?? feedback.Message;
            feedback.Rating = req.Rating ?? feedback.Rating;

            await _db.SaveChangesAsync();
            return Ok(feedback);
        }

        //Delete feedback
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var feedback = await _db.Feedbacks.FindAsync(id);
            if (feedback == null) return NotFound();

            _db.Feedbacks.Remove(feedback);
            await _db.SaveChangesAsync();

            return NoContent();
        }
    }

    //DTOs for course-level feedback
    public class FeedbackCreateRequest
    {
        public int LearnerId { get; set; }
        public int CourseId { get; set; }
        public string Message { get; set; } = null!;
        public int? Rating { get; set; }  // optional, default 0
    }

    public class FeedbackUpdateRequest
    {
        public string? Message { get; set; }
        public int? Rating { get; set; }
    }
}
