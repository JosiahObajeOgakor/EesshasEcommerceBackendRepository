using System.Threading.Tasks;
using Easshas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Easshas.WebApi.Controllers
{
    [ApiController]
    [Route("api/health")]
    [AllowAnonymous]
    public class HealthController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;
        private readonly AppDbContext _db;

        public HealthController(IConfiguration config, IWebHostEnvironment env, AppDbContext db)
        {
            _config = config;
            _env = env;
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var inMemory = _config.GetValue<bool>("App:UseInMemory");
            string dbStatus;
            try
            {
                var canConnect = await _db.Database.CanConnectAsync();
                dbStatus = canConnect ? "available" : "unavailable";
            }
            catch
            {
                dbStatus = "unavailable";
            }

            return Ok(new
            {
                status = "ok",
                environment = _env.EnvironmentName,
                inMemory,
                db = dbStatus
            });
        }
    }
}
