using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mini_LMS.Models;

namespace Mini_LMS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly MiniLMSContext _db;

        public UsersController(MiniLMSContext db)
        {
            _db = db;
        }



   
        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _db.Users
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Email,
                    u.Role,
                    u.IsActive,
                    u.CreatedAt
                })
                .ToListAsync();

            return Ok(users);
        }

   
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            var user = await _db.Users
                .Where(u => u.Id == id)
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Email,
                    u.Role,
                    u.IsActive,
                    u.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (user == null)
                return NotFound(new { message = "User not found" });

            return Ok(user);
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(
            int id,
            [FromForm] string username,
            [FromForm] string email,
            [FromForm] string role,
            [FromForm] bool is_active)
        {
            var existingUser = await _db.Users.FindAsync(id);
            if (existingUser == null)
                return NotFound(new { message = "User not found" });

            existingUser.Username = username;
            existingUser.Email = email;
            existingUser.Role = role;
            existingUser.IsActive = is_active;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "User updated successfully",
                user = new
                {
                    existingUser.Id,
                    existingUser.Username,
                    existingUser.Email,
                    existingUser.Role,
                    existingUser.IsActive,
                    existingUser.CreatedAt
                }
            });
        }

[HttpDelete("{id}")]
public async Task<IActionResult> DeleteUser(int id)
{

    var user = await _db.Users
        .Include(u => u.Passwordresets)
        .FirstOrDefaultAsync(u => u.Id == id);

    if (user == null)
        return NotFound(new { message = "User not found" });


    if (user.Passwordresets.Any())
        _db.Passwordresets.RemoveRange(user.Passwordresets);


    _db.Users.Remove(user);
    await _db.SaveChangesAsync();

    return Ok(new { message = "User and related resets deleted successfully" });
}


        [HttpPatch("{id}/toggle")]
        public async Task<IActionResult> ToggleUser(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null)
                return NotFound(new { message = "User not found" });

            user.IsActive = !user.IsActive;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = $"User status updated to {((bool)user.IsActive ? "Active" : "Inactive")}",
                user = new
                {
                    user.Id,
                    user.Username,
                    user.Email,
                    user.Role,
                    user.IsActive,
                    user.CreatedAt
                }
            });
        }


    }
}
