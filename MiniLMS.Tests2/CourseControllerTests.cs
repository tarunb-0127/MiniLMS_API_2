using NUnit.Framework;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Mini_LMS.Controllers;
using Mini_LMS.Models;
using Mini_LMS.Services;

namespace Mini_LMS.Tests.Controllers
{
    [TestFixture]
    public class CourseControllerTests
    {
        private CourseController _controller;
        private MiniLMSContext _context;
        private FakeEmailService _fakeEmail;
        private readonly NullLogger<CourseController> _logger
            = NullLogger<CourseController>.Instance;

        [SetUp]
        public void Setup()
        {
            // 1) Build an isolated EF InMemory provider
            var provider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            // 2) Configure DbContext to use InMemory + our provider
            var options = new DbContextOptionsBuilder<MiniLMSContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .UseInternalServiceProvider(provider)
                .Options;

            _context = new MiniLMSContext(options);

            // 3) Seed Trainer, Learner, and Admin users
            _context.Users.AddRange(
                new User
                {
                    Id = 1,
                    Email = "trainer@test.com",
                    PasswordHash = "h",
                    Role = "Trainer",
                    IsActive = true
                },
                new User
                {
                    Id = 2,
                    Email = "learner@test.com",
                    PasswordHash = "h",
                    Role = "Learner",
                    IsActive = true
                },
                new User
                {
                    Id = 3,
                    Email = "admin@test.com",
                    PasswordHash = "h",
                    Role = "Admin",
                    IsActive = true
                }
            );
            _context.SaveChanges();

            // 4) Swap in FakeEmailService
            _fakeEmail = new FakeEmailService();
            _controller = new CourseController(_context, _fakeEmail, _logger);

            // 5) Default principal = Trainer
            var trainerPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Email, "trainer@test.com"),
                new Claim(ClaimTypes.Role,  "Trainer")
            }, "mock"));
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = trainerPrincipal }
            };
        }

        [TearDown]
        public void Teardown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        // --- Create endpoint edge‐cases -----------------------------------

        [Test]
        public async Task Create_NoEmailClaim_ReturnsUnauthorized()
        {
            _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal();

            var dto = new CourseController.CourseCreateDTO { Name = "X", Type = "T", Duration = 1 };
            var result = await _controller.Create(dto) as UnauthorizedObjectResult;

            Assert.IsNotNull(result, "Expected 401 Unauthorized");
        }

        [Test]
        public async Task Create_UserNotTrainer_ReturnsUnauthorized()
        {
            var learnerPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Email, "learner@test.com"),
                new Claim(ClaimTypes.Role,  "Learner")
            }, "mock"));
            _controller.ControllerContext.HttpContext.User = learnerPrincipal;

            var dto = new CourseController.CourseCreateDTO { Name = "X", Type = "T", Duration = 1 };
            var result = await _controller.Create(dto) as UnauthorizedObjectResult;

            Assert.IsNotNull(result, "Learner should not be able to create");
        }

        // --- RequestTakedown endpoint edge‐cases --------------------------

        [Test]
        public async Task RequestTakedown_NonExistingCourse_ReturnsNotFound()
        {
            var dto = new CourseController.TakedownRequestDTO { CourseId = 999, Reason = "r" };
            var result = await _controller.RequestTakedown(dto) as NotFoundObjectResult;

            Assert.IsNotNull(result, "Expected 404 NotFound");
        }

        

        [Test]
        public async Task RequestTakedown_Valid_SendsNotificationAndEmail()
        {
            // seed one course with Id=10
            _context.Courses.Add(new Course
            {
                Id = 10,
                TrainerId = 1,
                Name = "ToRemove",
                Type = "T",
                Duration = 1,
                Visibility = "Public",
                IsApproved = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            _context.SaveChanges();

            var dto = new CourseController.TakedownRequestDTO { CourseId = 10, Reason = "Typo" };
            var ok = await _controller.RequestTakedown(dto) as OkObjectResult;

            Assert.IsNotNull(ok, "Expected 200 OK");
            var note = await _context.Notifications.SingleOrDefaultAsync();
            Assert.IsNotNull(note, "Should have one Notification");
            Assert.IsTrue(_fakeEmail.TakedownEmailSent, "FakeEmailService should have sent takedown email");
        }

        // --- Delete endpoint tests ----------------------------------------

        [Test]
        public async Task Delete_ExistingCourse_AsAdmin_ReturnsNoContent_AndDeletesCourse()
        {
            // seed one course with Id=20
            _context.Courses.Add(new Course
            {
                Id = 20,
                TrainerId = 1,
                Name = "DelMe",
                Type = "T",
                Duration = 1,
                Visibility = "Public",
                IsApproved = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            _context.SaveChanges();

            // switch principal to Admin
            var adminPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Email, "admin@test.com"),
                new Claim(ClaimTypes.Role,  "Admin")
            }, "mock"));
            _controller.ControllerContext.HttpContext.User = adminPrincipal;

            var result = await _controller.Delete(20);
            Assert.IsInstanceOf<NoContentResult>(result, "Expected 204 NoContent");
            Assert.IsNull(await _context.Courses.FindAsync(20), "Course should be removed");
        }

        [Test]
        public async Task Delete_NonExistingCourse_ReturnsNotFound()
        {
            var adminPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Email, "admin@test.com"),
                new Claim(ClaimTypes.Role,  "Admin")
            }, "mock"));
            _controller.ControllerContext.HttpContext.User = adminPrincipal;

            var result = await _controller.Delete(999) as NotFoundObjectResult;
            Assert.IsNotNull(result, "Expected 404 NotFound");
        }

        // --------------------------------------------------------------------
        // FakeEmailService overrides only the two methods used in these tests
        // --------------------------------------------------------------------
        private class FakeEmailService : EmailService
        {
            public bool NewCourseEmailSent { get; private set; }
            public bool TakedownEmailSent { get; private set; }

            public FakeEmailService()
                : base(
                    new ConfigurationBuilder()
                        .AddInMemoryCollection(new Dictionary<string, string>
                        {
                            { "Email:Username", "x@x.com" },
                            { "Email:Password", "p"          }
                        })
                        .Build(),
                    NullLogger<EmailService>.Instance
                  )
            {
            }

            public override Task<bool> SendNewCourseAvailableEmailAsync(string learnerEmail, string courseName)
            {
                NewCourseEmailSent = true;
                return Task.FromResult(true);
            }

            public override Task<bool> SendTakedownRequestEmailAsync(string adminEmail, string courseName, string trainerEmail)
            {
                TakedownEmailSent = true;
                return Task.FromResult(true);
            }
        }
    }
}
