using Microsoft.AspNetCore.Mvc;

namespace Mini_LMS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HomeController : ControllerBase
    {
        // GET: /api/welcome
        [HttpGet]
        public IActionResult Get()
        {
            var response = new
            {
                message = "Welcome to Mini Learning Management System!"
            };
            return Ok(response);
        }
    }
}
