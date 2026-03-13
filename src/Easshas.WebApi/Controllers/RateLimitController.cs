using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
namespace Easshas.WebApi.Controllers
{
    [ApiController]
    [Route("api/rate-limit")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "User,Admin")]
    public class RateLimitController : ControllerBase
    {
        private readonly IConfiguration _config;
        public RateLimitController(IConfiguration config) { _config = config; }

        [HttpGet("config")]
        public IActionResult GetConfig()
        {
            var permit = _config.GetValue<int?>("App:RateLimiting:AdminWrites:PermitLimit") ?? 10;
            var windowSeconds = _config.GetValue<int?>("App:RateLimiting:AdminWrites:WindowSeconds") ?? 60;
            return Ok(new
            {
                adminWrites = new
                {
                    permitLimit = permit,
                    windowSeconds,
                    partitionBy = "userId",
                    fallback = "remoteIp"
                }
            });
        }
    }
}
