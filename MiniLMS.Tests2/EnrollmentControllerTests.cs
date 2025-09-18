// src/Tests/Controllers/EnrollmentControllerTests.cs

using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniLMS.Controllers;
using Mini_LMS.Models;
using NUnit.Framework;

namespace Mini_LMS.Tests.Controllers
{
    // alias your Module EF entity so it doesn't collide with System.Reflection.Module
    using CourseModule = Mini_LMS.Models.Module;

    [TestFixture]
    public class EnrollmentControllerTests
    {
        private EnrollmentController _controller;
        private MiniLMSContext _context;

        [SetUp]
        public void Setup()
        {
            // 1) In-Memory EF Core
            var provider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            var options = new DbContextOptionsBuilder<MiniLMSContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .UseInternalServiceProvider(provider)
                .Options;

            _context = new MiniLMSContext(options);
            _context.Database.EnsureCreated();

            // 2) Seed users and a course
            var now = DateTime.UtcNow;
            _context.Users.AddRange(
                new User
                {
                    Id = 1,
                    Username = "trainer1",
                    Email = "trainer@test.com",
                    PasswordHash = "dummyhash",
                    Role = "Trainer",
                    IsActive = true,
                    CreatedAt = now
                },
                new User
                {
                    Id = 2,
                    Username = "learner1",
                    Email = "learner@test.com",
                    PasswordHash = "dummyhash",
                    Role = "Learner",
                    IsActive = true,
                    CreatedAt = now
                },
                new User
                {
                    Id = 3,
                    Username = "admin1",
                    Email = "admin@test.com",
                    PasswordHash = "dummyhash",
                    Role = "Admin",
                    IsActive = true,
                    CreatedAt = now
                }
            );

            _context.Courses.Add(new Course
            {
                Id = 100,
                TrainerId = 1,
                Name = "Course1",
                Type = "Tech",
                Duration = 5,
                Visibility = "Public",
                IsApproved = true,
                CreatedAt = now,
                UpdatedAt = now
            });

            _context.SaveChanges();

            // 3) Instantiate controller
            _controller = new EnrollmentController(_context);

            // 4) Simulate authenticated Learner#2
            var learnerIdentity = new ClaimsPrincipal(
                new ClaimsIdentity(new[] {
                    new Claim(ClaimTypes.NameIdentifier, "2"),
                    new Claim(ClaimTypes.Role,           "Learner")
                }, "mock"));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = learnerIdentity }
            };
        }

        [TearDown]
        public void Teardown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        // Reflection helper to read anonymous‐type props
        private static object GetProp(object obj, string propName)
        {
            var pi = obj.GetType()
                        .GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(pi, $"Property '{propName}' not found on {obj.GetType().Name}");
            return pi.GetValue(obj);
        }

        [Test]
        public async Task EnrollInCourse_ValidLearner_ReturnsOkAndPersistsEnrollment()
        {
            // Act
            var actionResult = await _controller.EnrollInCourse(100);
            var okResult = actionResult as OkObjectResult;
            Assert.NotNull(okResult, "Expected 200 OK");

            // Assert message
            var val = okResult.Value;
            var msg = GetProp(val, "message") as string;
            Assert.AreEqual("Enrolled successfully", msg);

            // Assert enrollment object
            var enrollment = GetProp(val, "enrollment") as Enrollment;
            Assert.NotNull(enrollment);
            Assert.AreEqual(2, enrollment.LearnerId);
            Assert.AreEqual(100, enrollment.CourseId);

            // Assert persisted
            var inDb = _context.Enrollments
                               .SingleOrDefault(e => e.LearnerId == 2 && e.CourseId == 100);
            Assert.NotNull(inDb);
        }

        [Test]
        public async Task GetMyCourses_ValidLearner_ReturnsOkWithCourseData()
        {
            // Arrange: seed one enrollment
            _context.Enrollments.Add(new Enrollment
            {
                LearnerId = 2,
                CourseId = 100,
                EnrolledAt = DateTime.UtcNow,
                Status = "Active"
            });
            _context.SaveChanges();

            // Act
            var actionResult = await _controller.GetMyCourses();
            var okResult = actionResult as OkObjectResult;
            Assert.NotNull(okResult, "Expected 200 OK");

            // Unpack and cast
            var raw = okResult.Value as IEnumerable;
            Assert.NotNull(raw, "Expected a list");
            var list = raw.Cast<object>().ToArray();
            Assert.AreEqual(1, list.Length);

            // Assert fields via reflection
            var first = list[0];
            Assert.AreEqual(100, (int)GetProp(first, "Id"));
            Assert.AreEqual("Course1", (string)GetProp(first, "Name"));
            Assert.AreEqual(5, (int)GetProp(first, "Duration"));
            Assert.AreEqual("trainer1", (string)GetProp(first, "Trainer"));
            Assert.AreEqual("Active", (string)GetProp(first, "Status"));
            Assert.That(GetProp(first, "EnrolledAt"), Is.TypeOf<DateTime>());
            Assert.Greater((int)GetProp(first, "EnrollmentId"), 0);
        }

        [Test]
        public async Task GetLearnersForCourse_AsTrainer_ReturnsOkWithLearners()
        {
            // Switch to Trainer#1
            var trainerIdentity = new ClaimsPrincipal(
                new ClaimsIdentity(new[] {
                    new Claim(ClaimTypes.NameIdentifier, "1"),
                    new Claim(ClaimTypes.Role,           "Trainer")
                }, "mock"));
            _controller.ControllerContext.HttpContext.User = trainerIdentity;

            // Seed another learner + enrollments
            _context.Users.Add(new User
            {
                Id = 4,
                Username = "learner2",
                Email = "learner2@test.com",
                PasswordHash = "dummyhash",
                Role = "Learner",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            _context.Enrollments.AddRange(
                new Enrollment { LearnerId = 2, CourseId = 100, EnrolledAt = DateTime.UtcNow, Status = "Active" },
                new Enrollment { LearnerId = 4, CourseId = 100, EnrolledAt = DateTime.UtcNow, Status = "Active" }
            );
            _context.SaveChanges();

            // Act
            var actionResult = await _controller.GetLearnersForCourse(100);
            var okResult = actionResult as OkObjectResult;
            Assert.NotNull(okResult, "Expected 200 OK");

            // Assert count
            var raw = okResult.Value as IEnumerable;
            Assert.NotNull(raw);
            Assert.AreEqual(2, raw.Cast<object>().Count());
        }

        [Test]
        public async Task DropEnrollment_LearnerOwn_ReturnsOkAndRemovesAllData()
        {
            // Seed module, progress, feedback, enrollment
            var module = new CourseModule
            {
                Id = 200,
                CourseId = 100,
                Name = "M1",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Modules.Add(module);

            _context.Moduleprogresses.Add(new Moduleprogress
            {
                LearnerId = 2,
                ModuleId = 200,
                ProgressPercentage = 75,
                IsCompleted = true,
                UpdatedAt = DateTime.UtcNow
            });
            _context.Feedbacks.Add(new Feedback
            {
                LearnerId = 2,
                CourseId = 100,
                Message = "Great course",
                SubmittedAt = DateTime.UtcNow
            });

            var enrollment = new Enrollment
            {
                LearnerId = 2,
                CourseId = 100,
                EnrolledAt = DateTime.UtcNow,
                Status = "Active"
            };
            _context.Enrollments.Add(enrollment);
            _context.SaveChanges();

            // Act
            var actionResult = await _controller.DropEnrollment(enrollment.Id);
            var okResult = actionResult as OkObjectResult;
            Assert.NotNull(okResult, "Expected 200 OK");

            var val = okResult.Value;
            var msg = GetProp(val, "message") as string;
            Assert.AreEqual(
                "Enrollment removed successfully, progress and feedbacks reset.",
                msg);

            // Verify deletion
            Assert.IsNull(await _context.Enrollments.FindAsync(enrollment.Id));
            Assert.IsEmpty(_context.Moduleprogresses
                .Where(mp => mp.LearnerId == 2 && mp.Module.CourseId == 100));
            Assert.IsEmpty(_context.Feedbacks
                .Where(f => f.LearnerId == 2 && f.CourseId == 100));
        }
    }
}
