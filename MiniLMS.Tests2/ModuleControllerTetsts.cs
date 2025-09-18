using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Mini_LMS.Controllers;
using Mini_LMS.Models;
using Mini_LMS.Services;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Mini_LMS.Tests.Controllers
{
    [TestFixture]
    public class ModuleControllerTests
    {
        private ModuleController _controller;
        private MiniLMSContext _context;
        private FakeEmailService _fakeEmail;
        private FakeBlobService _fakeBlob;
        private readonly NullLogger<ModuleController> _logger
            = NullLogger<ModuleController>.Instance;

        [SetUp]
        public void Setup()
        {
            // 1) Isolate EF Core with an InMemory provider
            var provider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            var options = new DbContextOptionsBuilder<MiniLMSContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .UseInternalServiceProvider(provider)
                .Options;

            _context = new MiniLMSContext(options);

            // 2) Seed a Trainer (Id=1), Learner (Id=2), Course (Id=10) and Enrollment
            _context.Users.AddRange(
                new User { Id = 1, Email = "trainer@test.com", PasswordHash = "h", Role = "Trainer", IsActive = true },
                new User { Id = 2, Email = "learner@test.com", PasswordHash = "h", Role = "Learner", IsActive = true }
            );

            _context.Courses.Add(new Course
            {
                Id = 10,
                TrainerId = 1,
                Name = "SampleCourse",
                Type = "T",
                Duration = 1,
                Visibility = "Public",
                IsApproved = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            _context.Enrollments.Add(new Enrollment
            {
                CourseId = 10,
                LearnerId = 2,
                EnrolledAt = DateTime.UtcNow
            });

            _context.SaveChanges();

            // 3) Replace real services with fakes
            _fakeEmail = new FakeEmailService();
            _fakeBlob = new FakeBlobService();

            // 4) Instantiate the controller under test
            _controller = new ModuleController(_context, _fakeEmail, _fakeBlob);

            // 5) Attach a JWT principal for the Trainer
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

        // CreateModule: unauthorized when no trainer claim
        [Test]
        public async Task CreateModule_NoTrainerClaim_ReturnsUnauthorized()
        {
            _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal();

            var dto = new ModuleController.ModuleCreateDto
            {
                CourseId = 10,
                Title = "Mod1",
                Content = "C1",
                File = null
            };

            var result = await _controller.CreateModule(dto);
            Assert.IsInstanceOf<UnauthorizedObjectResult>(result);
        }

        // CreateModule: course not found
        [Test]
        public async Task CreateModule_CourseNotFound_ReturnsNotFound()
        {
            var dto = new ModuleController.ModuleCreateDto
            {
                CourseId = 999,
                Title = "Mod1",
                Content = "C1",
                File = null
            };

            var result = await _controller.CreateModule(dto);
            Assert.IsInstanceOf<NotFoundObjectResult>(result);
        }

        // CreateModule: happy path, seeds progress + notification + email
        [Test]
        public async Task CreateModule_Valid_ReturnsCreatedAndInitializesProgress()
        {
            var dto = new ModuleController.ModuleCreateDto
            {
                CourseId = 10,
                Title = "Mod1",
                Content = "C1",
                File = null
            };

            var created = await _controller.CreateModule(dto) as CreatedAtActionResult;
            Assert.IsNotNull(created, "Expected 201 Created");
            Assert.AreEqual(nameof(ModuleController.GetModule), created.ActionName);

            var module = created.Value as Module;
            Assert.IsNotNull(module);
            Assert.AreEqual(10, module.CourseId);
            Assert.AreEqual("Mod1", module.Name);
            Assert.AreEqual("C1", module.Description);
            Assert.IsNull(module.FilePath);

            // Progress seeded for learner Id=2
            var prog = _context.Moduleprogresses
                        .Single(p => p.ModuleId == module.Id && p.LearnerId == 2);
            Assert.AreEqual(0, prog.ProgressPercentage);
            Assert.IsFalse(prog.IsCompleted);

            // Notification + email sent
            var note = _context.Notifications.Single(n => n.Type == "ModuleUpdate");
            Assert.IsTrue(_fakeEmail.CourseUpdateEmailSent);
        }

        // UpdateModule: resets progress and notifies
        [Test]
        public async Task UpdateModule_Valid_ResetsProgressAndReturnsOk()
        {
            // seed module and progress
            var module = new Module
            {
                Id = 20,
                CourseId = 10,
                Name = "Old",
                Description = "Desc",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Modules.Add(module);
            _context.Moduleprogresses.Add(new Moduleprogress
            {
                ModuleId = 20,
                LearnerId = 2,
                ProgressPercentage = 50,
                IsCompleted = true,
                UpdatedAt = DateTime.UtcNow
            });
            _context.SaveChanges();

            var dto = new ModuleController.ModuleUpdateDto
            {
                Title = "New",
                Content = "NewDesc",
                File = null
            };

            var ok = await _controller.UpdateModule(20, dto) as OkObjectResult;
            Assert.IsNotNull(ok, "Expected 200 OK");

            var updated = ok.Value as Module;
            Assert.AreEqual("New", updated.Name);
            Assert.AreEqual("NewDesc", updated.Description);

            // Progress reset
            var prog = _context.Moduleprogresses.Single(p => p.ModuleId == 20);
            Assert.AreEqual(0, prog.ProgressPercentage);
            Assert.IsFalse(prog.IsCompleted);

            Assert.IsTrue(_fakeEmail.CourseUpdateEmailSent);
        }

        // DeleteModule: valid delete, no content + cleans up progress
        [Test]
        public async Task DeleteModule_Valid_ReturnsNoContentAndDeletes()
        {
            // seed module + progress
            var module = new Module
            {
                Id = 30,
                CourseId = 10,
                Name = "ToDel",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Modules.Add(module);
            _context.Moduleprogresses.Add(new Moduleprogress
            {
                ModuleId = 30,
                LearnerId = 2,
                UpdatedAt = DateTime.UtcNow
            });
            _context.SaveChanges();

            var result = await _controller.DeleteModule(30);
            Assert.IsInstanceOf<NoContentResult>(result);

            Assert.IsNull(await _context.Modules.FindAsync(30));
            Assert.IsEmpty(_context.Moduleprogresses.Where(p => p.ModuleId == 30));
            Assert.IsTrue(_fakeEmail.CourseUpdateEmailSent);
        }

        // GetModule: not found
        [Test]
        public async Task GetModule_NonExisting_ReturnsNotFound()
        {
            var result = await _controller.GetModule(999);
            Assert.IsInstanceOf<NotFoundResult>(result);
        }

        // GetModule: found
        [Test]
        public async Task GetModule_Existing_ReturnsOkWithModule()
        {
            var m = new Module
            {
                Id = 40,
                CourseId = 10,
                Name = "M1",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Modules.Add(m);
            _context.SaveChanges();

            var ok = await _controller.GetModule(40) as OkObjectResult;
            Assert.IsNotNull(ok);
            Assert.AreEqual("M1", (ok.Value as Module)!.Name);
        }

        

        // --------------------------------------------------------------------
        // Fakes for EmailService and BlobService
        // --------------------------------------------------------------------
        private class FakeEmailService : EmailService
        {
            public bool CourseUpdateEmailSent { get; private set; }

            public FakeEmailService()
                : base(new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                        .AddInMemoryCollection(new Dictionary<string, string> {
                            { "Email:Username", "x@x.com" },
                            { "Email:Password", "p" }
                        })
                        .Build(),
                       NullLogger<EmailService>.Instance)
            { }

            public override Task<bool> SendCourseUpdateEmailAsync(string trainerEmail, string message)
            {
                CourseUpdateEmailSent = true;
                return Task.FromResult(true);
            }
        }

        private class FakeBlobService : BlobService
        {
            public FakeBlobService()
                : base(new BlobContainerClient(
                        // any valid URI works; we never call the base UploadAsync
                        new Uri("https://fake.blob/test-container"),
                        new BlobClientOptions()))
            { }

            public override Task<string> UploadAsync(IFormFile file)
            {
                return Task.FromResult($"https://fake.blob/{file.FileName}");
            }
        }
    }
}
