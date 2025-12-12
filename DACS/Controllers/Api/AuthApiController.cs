using DACS.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DACS.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthApiController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AuthApiController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // POST: api/authapi/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] ApiLoginModel model)
        {
            var user = await _userManager.FindByNameAsync(model.Username);
            if (user != null && await _userManager.CheckPasswordAsync(user, model.Password))
            {
                // Lấy Role để Mobile biết là Khách hay Admin
                var roles = await _userManager.GetRolesAsync(user);

                return Ok(new
                {
                    Status = "Success",
                    UserId = user.Id,
                    FullName = user.FullName, // Giả sử model ApplicationUser có field này
                    Email = user.Email,
                    Role = roles.FirstOrDefault()
                });
            }
            return Unauthorized(new { Status = "Error", Message = "Sai tài khoản hoặc mật khẩu" });
        }
    }

    // Class DTO để nhận dữ liệu từ Mobile
    public class ApiLoginModel  // <--- Đổi từ LoginModel thành ApiLoginModel
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }
}