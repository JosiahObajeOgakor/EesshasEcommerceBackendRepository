using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Easshas.Application.Abstractions;

namespace Easshas.WebApi.Controllers
{
    [ApiController]
    [Route("api/orders")] // e.g. /api/orders/{trackingNumber}/status
    public class OrderTrackingController : ControllerBase
    {
        private readonly IOrderService _orders;
        public OrderTrackingController(IOrderService orders)
        {
            _orders = orders;
        }

        // Get order status and history by tracking number (for user or guest)
        [HttpGet("{trackingNumber}/status")]
        [AllowAnonymous]
        public async Task<IActionResult> GetStatus(string trackingNumber)
        {
            var order = await _orders.GetByTrackingNumberAsync(trackingNumber);
            if (order == null)
                return NotFound(new { message = "Order not found" });
            return Ok(new
            {
                trackingNumber = order.TrackingNumber,
                status = order.Status.ToString(),
                history = order.StatusHistory.OrderBy(h => h.ChangedAt).Select(h => new {
                    status = h.Status.ToString(),
                    changedAt = h.ChangedAt,
                    note = h.Note
                })
            });
        }
    }
}
