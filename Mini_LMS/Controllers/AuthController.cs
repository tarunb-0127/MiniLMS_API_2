using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mini_LMS.Helpers;
using Mini_LMS.Models;
using Mini_LMS.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mini_LMS.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private const string AdminEmail = "tarun.balaji@relevantz.com";
        private const string AdminPassword = "Admin@123";

        private static readonly Dictionary<string, string> AdminOtps = new();

        private readonly MiniLMSContext _db;
        private readonly EmailService _emailService;
        private readonly JwtHelper _jwtHelper;

        public AuthController(MiniLMSContext db, EmailService emailService, JwtHelper jwtHelper)
        {
            _db = db;
            _emailService = emailService;
            _jwtHelper = jwtHelper;
        }

        // ─── ADMIN LOGIN STEP 1: Send OTP ───────────────────────────────
        [HttpPost("login/admin")]
    
        public async Task<IActionResult> AdminLogin([FromForm] string email, [FromForm] string password)
        {
            if (email != AdminEmail || password != AdminPassword)
                return Unauthorized(new { message = "Invalid admin credentials." });

            var otp = new Random().Next(100000, 999999).ToString();
            AdminOtps[email] = otp;

            await _emailService.SendOtpEmailAsync(email, otp);
            return Ok(new { message = "OTP sent to admin email." });
        }

        // ─── ADMIN LOGIN STEP 2: Verify OTP ─────────────────────────────
        [HttpPost("admin/verify-otp")]
        [AllowAnonymous]
        public IActionResult VerifyAdminOtp([FromForm] string email, [FromForm] string otp)
        {
            if (!AdminOtps.ContainsKey(email) || AdminOtps[email] != otp)
                return Unauthorized(new { message = "Invalid or expired OTP." });

            AdminOtps.Remove(email);
            var token = _jwtHelper.GenerateToken(email, "Admin", 0);
            return Ok(new { message = "OTP verified. Admin login successful.", token });
        }

        // ─── USER LOGIN ─────────────────────────────────────────────────
        [HttpPost("login/user")]
        [AllowAnonymous]
        public async Task<IActionResult> UserLogin([FromForm] string email, [FromForm] string password, [FromForm] string role)
        {
            var user = await _db.Users
                .SingleOrDefaultAsync(u => u.Email == email && u.Role == role);

            if (user == null || !user.IsActive.GetValueOrDefault() || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                return Unauthorized(new { message = $"Invalid {role} credentials." });

            var token = _jwtHelper.GenerateToken(user.Email, user.Role, (int)user.Id);
            return Ok(new { message = $"{role} login successful.", token });
        }

        // ─── USER REGISTRATION ──────────────────────────────────────────
        // ─── USER REGISTRATION ──────────────────────────────────────────
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register(
            [FromForm] string username,   // <-- added username
            [FromForm] string email,
            [FromForm] string password,
            [FromForm] string role)
        {
            if (string.IsNullOrWhiteSpace(username))
                return BadRequest(new { message = "Username is required." });

            if (await _db.Users.AnyAsync(u => u.Email == email))
                return Conflict(new { message = "Email already registered." });

            if (role != "Trainer" && role != "Learner")
                return BadRequest(new { message = "Invalid role. Must be 'Trainer' or 'Learner'." });

            var user = new User
            {
                Username = username,                              // save username
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = role,
                IsActive = false,
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return Ok(new { message = $"{role} '{username}' registered successfully." });
        }


        // ─── PASSWORD RESET REQUEST ─────────────────────────────────────
        [HttpPost("password-reset/request")]
        [AllowAnonymous]
        public async Task<IActionResult> RequestPasswordReset([FromForm] int userId)
        {
            var user = await _db.Users.FindAsync(userId);
            

            var token = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;

            _db.Passwordresets.Add(new Passwordreset
            {
                UserId = userId,
                Email = user.Email,
                Token = token,
                SentAt = now,
                ExpiryTime = now.AddMinutes(30)
            });

            await _db.SaveChangesAsync();

            string link = string.IsNullOrEmpty(user.PasswordHash)
                ? $"http://localhost:5173/setup-password?userId={userId}&token={token}"
                : $"http://localhost:5173/reset-password?userId={userId}&token={token}";

            await _emailService.SendPasswordResetEmailAsync(user.Email, link);
            return Ok(new { message = "Password reset/setup link sent to user email." });
        }

        // ─── PASSWORD RESET CONFIRM ─────────────────────────────────────
        [HttpPost("password-reset/reset")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword(
            [FromForm] string email,
            [FromForm] string token,
            [FromForm] string newPassword,
            [FromForm] string confirmPassword)
        {
            if (newPassword != confirmPassword)
                return BadRequest(new { message = "Passwords do not match." });

            var reset = await _db.Passwordresets
                .Where(x => x.Email == email && x.Token == token)
                .OrderByDescending(x => x.SentAt)
                .FirstOrDefaultAsync();

            if (reset == null || reset.ExpiryTime < DateTime.UtcNow)
                return Unauthorized(new { message = "Invalid or expired token." });

            var user = await _db.Users.SingleOrDefaultAsync(u => u.Email == email);
            if (user == null)
                return NotFound(new { message = "User not found." });

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Password reset successful." });
        }

        // 🔐 Only Admins can get these stats
        [Authorize(Roles = "Admin")]
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var totalCourses = await _db.Courses.CountAsync();
            var totalUsers = await _db.Users.CountAsync();
            var activeUsers = await _db.Users.CountAsync(u => u.IsActive == true);
            var totalTrainers = await _db.Users.CountAsync(u => u.Role == "Trainer");
            var totalLearners = await _db.Users.CountAsync(u => u.Role == "Learner");

            // If you have a takedown table:
            var takedownRequests = await _db.CourseTakedownRequests.CountAsync();

            return Ok(new
            {
                totalCourses,
                totalUsers,
                activeUsers,
                totalTrainers,
                totalLearners,
                takedownRequests
            });
        }


        // 🔐 Only authenticated users with Admin role can access
        [Authorize(Roles = "Admin")]
        [HttpGet("home")]
        public IActionResult GetAdminHome()
        {
            return Ok(new
            {
                message = "Welcome to the Admin Dashboard",
                user = User.Identity?.Name
            });
        }

        
        
    }
}




