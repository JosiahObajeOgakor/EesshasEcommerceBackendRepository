using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Easshas.WebApi.RealTime;
using Microsoft.AspNetCore.Authentication.JwtBearer;
namespace Easshas.WebApi.Controllers
{
    [ApiController]
    [Route("api/tracking")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "User,Admin")]    public class TrackingController : ControllerBase
    {
        private readonly IHubContext<TrackingHub> _hub;
        public TrackingController(IHubContext<TrackingHub> hub)
        {
            _hub = hub;
        }

        public record LocationUpdate(Guid OrderId, double Latitude, double Longitude, DateTime? Timestamp);

        [HttpPost]
        // or delivery role in a real system
        public async Task<IActionResult> UpdateLocation([FromBody] LocationUpdate update)
        {
            var payload = new { orderId = update.OrderId, latitude = update.Latitude, longitude = update.Longitude, timestamp = update.Timestamp ?? DateTime.UtcNow };
            await _hub.Clients.Group($"order:{update.OrderId}").SendAsync("location", payload);
            return Accepted(payload);
        }
    }
}
