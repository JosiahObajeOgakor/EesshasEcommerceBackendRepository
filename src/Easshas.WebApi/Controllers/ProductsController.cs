using System;
using System.Linq;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Easshas.WebApi.Controllers
{
    [ApiController]
    [Route("api/products")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _products;
        private readonly IOrderService _orders;
        public ProductsController(IProductService products, IOrderService orders) { _products = products; _orders = orders; }

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] int skip = 0, [FromQuery] int take = 50)
        {
            var items = await _products.ListAsync(skip, take);
            var active = items.Where(p => p.Active);
            return Ok(active);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var p = await _products.GetByIdAsync(id);
            return p is null ? NotFound() : Ok(p);
        }

        [HttpGet("{id:guid}/stats")]
        public async Task<IActionResult> GetStats(Guid id)
        {
            var p = await _products.GetByIdAsync(id);
            if (p is null) return NotFound();
            var purchased = await _orders.GetProductPurchaseCountAsync(id);
            return Ok(new { purchases = purchased, remainingInventory = p.Inventory, available = p.Available });
        }
    }
}
