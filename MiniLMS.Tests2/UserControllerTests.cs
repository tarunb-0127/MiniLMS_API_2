// src/Tests/Controllers/UsersControllerTests.cs

using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mini_LMS.Controllers;
using Mini_LMS.Models;
using NUnit.Framework;

namespace Mini_LMS.Tests.Controllers
{
    [TestFixture]
    public class UsersControllerTests
    {
        private MiniLMSContext _context;
        private UsersController _controller;

        [SetUp]
        public void SetUp()
        {
            var provider = new ServiceCollection()
              .AddEntityFrameworkInMemoryDatabase()
              .BuildServiceProvider();

            var options = new DbContextOptionsBuilder<MiniLMSContext>()
              .UseInMemoryDatabase(Guid.NewGuid().ToString())
              .UseInternalServiceProvider(provider)
              .Options;

            _context = new MiniLMSContext(options);
            _context.Database.EnsureCreated();

            var now = DateTime.UtcNow;

            _context.Users.AddRange(
              new User
              {
                  Id = 1,
                  Username = "alice",
                  Email = "alice@test.com",
                  PasswordHash = "hash",
                  Role = "Trainer",
                  IsActive = true,
                  CreatedAt = now
              },
              new User
              {
                  Id = 2,
                  Username = "bob",
                  Email = "bob@test.com",
                  PasswordHash = "hash",
                  Role = "Learner",
                  IsActive = false,
                  CreatedAt = now.AddDays(-1)
              }
            );

            _context.SaveChanges();

            _controller = new UsersController(_context);
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        // Reflection helper to read anonymous‐type props
        private static object GetProp(object obj, string name)
        {
            var pi = obj.GetType()
                        .GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(pi, $"Property '{name}' not found on {obj.GetType().Name}");
            return pi.GetValue(obj)!;
        }

        [Test]
        public async Task GetUsers_ReturnsAllUsers()
        {
            var result = await _controller.GetUsers() as OkObjectResult;
            Assert.NotNull(result);

            var raw = result.Value as IEnumerable;
            Assert.NotNull(raw);

            var arr = raw.Cast<object>().ToArray();
            Assert.AreEqual(2, arr.Length);

            // verify first user has correct shape
            var u1 = arr[0];
            Assert.AreEqual(1, (int)GetProp(u1, "Id"));
            Assert.AreEqual("alice", (string)GetProp(u1, "Username"));
            Assert.AreEqual("alice@test.com", (string)GetProp(u1, "Email"));
            Assert.AreEqual("Trainer", (string)GetProp(u1, "Role"));
            Assert.AreEqual(true, (bool)GetProp(u1, "IsActive"));
            Assert.That(GetProp(u1, "CreatedAt"), Is.TypeOf<DateTime>());
        }

        [Test]
        public async Task GetUser_ExistingId_ReturnsOk()
        {
            var result = await _controller.GetUser(1) as OkObjectResult;
            Assert.NotNull(result);

            var u = result.Value!;
            Assert.AreEqual(1, (int)GetProp(u, "Id"));
            Assert.AreEqual("alice", (string)GetProp(u, "Username"));
            Assert.AreEqual("alice@test.com", (string)GetProp(u, "Email"));
            Assert.AreEqual("Trainer", (string)GetProp(u, "Role"));
            Assert.AreEqual(true, (bool)GetProp(u, "IsActive"));
        }

        [Test]
        public async Task GetUser_NonExisting_ReturnsNotFound()
        {
            var result = await _controller.GetUser(999) as NotFoundObjectResult;
            Assert.NotNull(result);

            var body = result.Value!;
            var msg = (string)GetProp(body, "message");
            Assert.AreEqual("User not found", msg);
        }

        [Test]
        public async Task UpdateUser_Existing_UpdatesAndReturnsOk()
        {
            var newName = "alice2";
            var newEmail = "alice2@test.com";
            var newRole = "Admin";
            var newActive = false;

            var result = await _controller.UpdateUser(
              1,
              newName,
              newEmail,
              newRole,
              newActive
            ) as OkObjectResult;
            Assert.NotNull(result);

            var body = result.Value!;
            var msg = (string)GetProp(body, "message");
            Assert.AreEqual("User updated successfully", msg);

            var u = GetProp(body, "user");
            Assert.AreEqual(1, (int)GetProp(u, "Id"));
            Assert.AreEqual(newName, (string)GetProp(u, "Username"));
            Assert.AreEqual(newEmail, (string)GetProp(u, "Email"));
            Assert.AreEqual(newRole, (string)GetProp(u, "Role"));
            Assert.AreEqual(newActive, (bool)GetProp(u, "IsActive"));

            // verify persisted
            var inDb = await _context.Users.FindAsync(1);
            Assert.AreEqual(newName, inDb!.Username);
            Assert.AreEqual(newEmail, inDb.Email);
            Assert.AreEqual(newRole, inDb.Role);
            Assert.AreEqual(newActive, inDb.IsActive);
        }

        [Test]
        public async Task UpdateUser_NonExisting_ReturnsNotFound()
        {
            var result = await _controller.UpdateUser(
              999, "x", "x", "x", true
            ) as NotFoundObjectResult;
            Assert.NotNull(result);
            Assert.AreEqual("User not found", (string)GetProp(result.Value!, "message"));
        }

        
        

        [Test]
        public async Task DeleteUser_NonExisting_ReturnsNotFound()
        {
            var result = await _controller.DeleteUser(999) as NotFoundObjectResult;
            Assert.NotNull(result);
            Assert.AreEqual("User not found", (string)GetProp(result.Value!, "message"));
        }

        [Test]
        public async Task ToggleUser_Existing_TogglesAndReturnsOk()
        {
            // user 2 is initially inactive
            var result = await _controller.ToggleUser(2) as OkObjectResult;
            Assert.NotNull(result);

            var body = result.Value!;
            var msg = (string)GetProp(body, "message");
            Assert.AreEqual("User status updated to Active", msg);

            var u = GetProp(body, "user");
            Assert.AreEqual(2, (int)GetProp(u, "Id"));
            Assert.AreEqual(true, (bool)GetProp(u, "IsActive"));

            // verify persisted
            Assert.IsTrue((await _context.Users.FindAsync(2))!.IsActive);
        }

        [Test]
        public async Task ToggleUser_NonExisting_ReturnsNotFound()
        {
            var result = await _controller.ToggleUser(999) as NotFoundObjectResult;
            Assert.NotNull(result);
            Assert.AreEqual("User not found", (string)GetProp(result.Value!, "message"));
        }
    }
}
