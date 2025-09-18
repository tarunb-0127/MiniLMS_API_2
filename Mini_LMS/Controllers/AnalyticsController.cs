using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mini_LMS.Models;
using Mini_LMS.DTOs;

namespace Mini_LMS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnalyticsController : ControllerBase
    {
        private readonly MiniLMSContext _db;

        public AnalyticsController(MiniLMSContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Get analytics data for a specific trainer (courses, learners, ratings, progress).
        /// </summary>
        [HttpGet("trainer/{trainerId:long}")]
        public async Task<IActionResult> GetTrainerAnalytics(long trainerId)
        {
            // ✅ Fetch courses with learners, ratings, and progress
            var courses = await _db.Courses
                .Where(c => c.TrainerId == trainerId)
                .Select(c => new CourseAnalyticsDto
                {
                    Id = c.Id,
                    Name = c.Name,

                    LearnerCount = _db.Enrollments
                        .Count(e => e.CourseId == c.Id),

                    AvgRating = _db.Feedbacks
                        .Where(f => f.CourseId == c.Id)
                        .Select(f => (double?)f.Rating)
                        .Average() ?? 0,

                    AvgProgress = (from mp in _db.Moduleprogresses
                                   join m in _db.Modules on mp.ModuleId equals m.Id
                                   where m.CourseId == c.Id
                                   select (double?)mp.ProgressPercentage)
                                   .Average() ?? 0
                })
                .ToListAsync();

            var response = new TrainerAnalyticsDto
            {
                TotalCourses = courses.Count,
                TotalLearners = courses.Sum(c => c.LearnerCount),
                Courses = courses
            };

            return Ok(response);
        }

        // GET: api/Analytics/trainer/{trainerId}/learners
[HttpGet("trainer/{trainerId}/learners")]
public async Task<IActionResult> GetTrainerLearners(long trainerId)
{
    var learners = await (from e in _db.Enrollments
                          join u in _db.Users on e.LearnerId equals u.Id
                          join c in _db.Courses on e.CourseId equals c.Id
                          where c.TrainerId == trainerId && u.Role == "Learner"
                          select new
                          {
                              LearnerId = u.Id,
                              LearnerName = u.Username,
                              LearnerEmail = u.Email,
                              CourseName = c.Name,
                              Progress = (from mp in _db.Moduleprogresses
                                          join m in _db.Modules on mp.ModuleId equals m.Id
                                          where m.CourseId == c.Id && mp.LearnerId == u.Id
                                          select (double?)mp.ProgressPercentage).Average() ?? 0
                          }).ToListAsync();

    // Group learners by LearnerId
    var result = learners
        .GroupBy(l => new { l.LearnerId, l.LearnerName, l.LearnerEmail })
        .Select(g => new
        {
            g.Key.LearnerId,
            g.Key.LearnerName,
            g.Key.LearnerEmail,
            Courses = g.Select(x => new { x.CourseName, x.Progress }).ToList()
        });

    return Ok(result);
}

    }
}
