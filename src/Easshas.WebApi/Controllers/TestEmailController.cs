using System;
using Microsoft.AspNetCore.Mvc;
using Easshas.Application.Abstractions;
using Easshas.Domain.Entities;
using Easshas.Domain.ValueObjects;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Easshas.WebApi.Controllers
{
    [ApiController]
    [Route("api/test-email")]
    public class TestEmailController : ControllerBase
    {
        private readonly IEmailSender _emailSender;

        public TestEmailController(IEmailSender emailSender)
        {
            _emailSender = emailSender;
        }

        [HttpPost("send-test")]
        public async Task<IActionResult> SendTestEmail([FromQuery] string recipient)
        {
            Console.WriteLine($"Injected EmailSender type: {_emailSender.GetType().FullName}");
            var testOrder = new Order
            {
                Id = Guid.NewGuid(),
                TrackingNumber = "TEST-TRK-999",
                TotalAmount = 5000,
                Currency = "NGN",
                UserId = Guid.NewGuid(),
                BillingAddress = new Address(
                    "Test User",
                    "123 Test Street",
                    null,
                    "Lagos",
                    "Lagos",
                    "Nigeria",
                    "100001",
                    "08012345678"
                ),
                Items = new List<OrderItem>
                {
                    new OrderItem
                    {
                        ProductId = Guid.NewGuid(),
                        NameSnapshot = "Branded T-Shirt",
                        Quantity = 2,
                        UnitPrice = 2500
                    }
                }
            };

            await _emailSender.SendOrderConfirmationAsync(testOrder, recipient, "support@Springuptechafrica.com");
            return Ok(new { message = "Test email sent to " + recipient });
        }
    }
}
