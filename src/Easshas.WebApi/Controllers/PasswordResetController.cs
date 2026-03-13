using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Easshas.WebApi.Controllers
{
    [ApiController]
    [Route("api/auth/password")]
    public class PasswordResetController : ControllerBase
    {
        private readonly IPasswordResetService _passwordReset;

        public PasswordResetController(IPasswordResetService passwordReset)
        {
            _passwordReset = passwordReset;
        }

        [HttpPost("request")]
        public async Task<IActionResult> RequestReset([FromBody] RequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.Email)) return BadRequest(new { message = "Email required" });
            var ok = await _passwordReset.SendResetOtpAsync(dto.Email.Trim());
            if (!ok) return NotFound(new { message = "User not found or email send failed" });
            return Ok(new { message = "OTP sent if the email exists" });
        }

        [HttpPost("confirm")]
        public async Task<IActionResult> ConfirmReset([FromBody] ConfirmDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Otp) || string.IsNullOrWhiteSpace(dto.NewPassword))
                return BadRequest(new { message = "Email, otp and newPassword required" });

            var ok = await _passwordReset.VerifyOtpAndResetPasswordAsync(dto.Email.Trim(), dto.Otp.Trim(), dto.NewPassword);
            if (!ok) return BadRequest(new { message = "OTP invalid/expired or password reset failed" });
            return Ok(new { message = "Password reset successful" });
        }

        public record RequestDto(string Email);
        public record ConfirmDto(string Email, string Otp, string NewPassword);
    }
}
