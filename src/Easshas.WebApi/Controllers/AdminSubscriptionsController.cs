using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
namespace Easshas.WebApi.Controllers
{
    [ApiController]
    [Route("api/admin/subscriptions")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "User,Admin")]
    public class AdminSubscriptionsController : ControllerBase
    {
        private readonly IAdminSubscriptionService _subs;
        public AdminSubscriptionsController(IAdminSubscriptionService subs)
        {
            _subs = subs;
        }

        public record InitRequest(decimal Amount, string Email, string CallbackUrl, Guid? AdminId);

        [HttpPost("init")]
        public async Task<IActionResult> Init([FromBody] InitRequest req)
        {
            // Prefer bearer claims if present, else fallback to AdminId provided in body
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid adminId;
            if (!string.IsNullOrWhiteSpace(idStr) && Guid.TryParse(idStr, out adminId))
            {
                // ok: from claims
            }
            else if (req.AdminId.HasValue)
            {
                adminId = req.AdminId.Value;
            }
            else
            {
                return BadRequest(new { message = "Missing admin identity. Provide AdminId or sign in as admin." });
            }

            var sub = await _subs.InitializeAsync(adminId, req.Amount, req.Email, req.CallbackUrl);
            return Ok(new { reference = sub.Reference });
        }

        [HttpPost("verify")] // Alternatively use a dedicated webhook
        [AllowAnonymous]
        public async Task<IActionResult> Verify([FromQuery] string reference)
        {
            var ok = await _subs.VerifyAndActivateAsync(reference);
            return ok ? Ok(new { message = "Subscription active" }) : BadRequest(new { message = "Verification failed" });
        }
    }
}
