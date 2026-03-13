
// ...no code above using directives...
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Easshas.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Http;
using Easshas.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Easshas.WebApi.Controllers
{
    [ApiController]
    [Route("api/admin/products")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "User,Admin")]
    public class AdminProductsController : ControllerBase
    {
        private readonly IProductService _products;
        private readonly S3Service _s3;
        public AdminProductsController(IProductService products, S3Service s3)
        {
            _products = products;
            _s3 = s3;
        }

        public class ProductCreateRequest
        {
            [Required]
            [StringLength(100, MinimumLength = 2)]
            public string Name { get; set; } = string.Empty;

            [Required]
            [StringLength(2000)]
            public string Description { get; set; } = string.Empty;

            [StringLength(100)]
            public string? Category { get; set; }

            [Range(0.01, 100000000)]
            public decimal Price { get; set; }

            [Required]
            [StringLength(100)]
            public string BrandName { get; set; } = string.Empty;

            [StringLength(100)]
            public string? Sku { get; set; }

            [Range(0, int.MaxValue)]
            public int Inventory { get; set; } = 0;

            public bool Available { get; set; } = true;

            public Guid? CategoryId { get; set; }

            // Image files
            public IFormFile? Image1 { get; set; }
            public IFormFile? Image2 { get; set; }
            public IFormFile? Image3 { get; set; }
        }

        [HttpPost]
        [EnableRateLimiting("AdminWrites")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create([FromForm] ProductCreateRequest dto)
        {
            // Prevent duplicates by Name (case-insensitive)
            var existing = await _products.GetByNameAsync(dto.Name);
            if (existing != null)
            {
                return Conflict(new { message = "Product with the same name already exists.", id = existing.Id });
            }
            // Prevent duplicates by SKU (case-insensitive)
            if (!string.IsNullOrWhiteSpace(dto.Sku))
            {
                var bySku = await _products.GetBySkuAsync(dto.Sku);
                if (bySku != null)
                {
                    return Conflict(new { message = "Product with the same SKU already exists.", id = bySku.Id });
                }
            }
            if (dto.CategoryId.HasValue)
            {
                var catExists = await _products.CategoryExistsAsync(dto.CategoryId.Value);
                if (!catExists)
                {
                    return BadRequest(new { message = "Invalid categoryId." });
                }
            }

            // Upload images to S3
            string? imageUrl1 = null, imageUrl2 = null, imageUrl3 = null;
            if (dto.Image1 != null && dto.Image1.Length > 0)
            {
                var fileName = $"products/{Guid.NewGuid()}_{dto.Image1.FileName}";
                using var stream = dto.Image1.OpenReadStream();
                imageUrl1 = await _s3.UploadFileAsync(stream, fileName, dto.Image1.ContentType);
            }
            if (dto.Image2 != null && dto.Image2.Length > 0)
            {
                var fileName = $"products/{Guid.NewGuid()}_{dto.Image2.FileName}";
                using var stream = dto.Image2.OpenReadStream();
                imageUrl2 = await _s3.UploadFileAsync(stream, fileName, dto.Image2.ContentType);
            }
            if (dto.Image3 != null && dto.Image3.Length > 0)
            {
                var fileName = $"products/{Guid.NewGuid()}_{dto.Image3.FileName}";
                using var stream = dto.Image3.OpenReadStream();
                imageUrl3 = await _s3.UploadFileAsync(stream, fileName, dto.Image3.ContentType);
            }

            var entity = new Product
            {
                Name = dto.Name,
                Description = dto.Description,
                Category = dto.Category ?? string.Empty,
                Price = dto.Price,
                BrandName = dto.BrandName,
                Sku = dto.Sku,
                Inventory = dto.Inventory,
                Available = dto.Available,
                ImageUrl1 = imageUrl1,
                ImageUrl2 = imageUrl2,
                ImageUrl3 = imageUrl3,
                CategoryId = dto.CategoryId
            };
            var created = await _products.CreateAsync(entity);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var p = await _products.GetByIdAsync(id);
            return p is null ? NotFound() : Ok(p);
        }

        [HttpPut("{id:guid}")]
        [EnableRateLimiting("AdminWrites")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Update(Guid id, [FromForm] ProductCreateRequest dto)
        {
            // Ensure SKU uniqueness if provided
            if (!string.IsNullOrWhiteSpace(dto.Sku))
            {
                var bySku = await _products.GetBySkuAsync(dto.Sku);
                if (bySku != null && bySku.Id != id)
                {
                    return Conflict(new { message = "Product with the same SKU already exists.", id = bySku.Id });
                }
            }
            if (dto.CategoryId.HasValue)
            {
                var catExists = await _products.CategoryExistsAsync(dto.CategoryId.Value);
                if (!catExists)
                {
                    return BadRequest(new { message = "Invalid categoryId." });
                }
            }

            // Upload images to S3 if provided
            string? imageUrl1 = null, imageUrl2 = null, imageUrl3 = null;
            if (dto.Image1 != null && dto.Image1.Length > 0)
            {
                var fileName = $"products/{Guid.NewGuid()}_{dto.Image1.FileName}";
                using var stream = dto.Image1.OpenReadStream();
                imageUrl1 = await _s3.UploadFileAsync(stream, fileName, dto.Image1.ContentType);
            }
            if (dto.Image2 != null && dto.Image2.Length > 0)
            {
                var fileName = $"products/{Guid.NewGuid()}_{dto.Image2.FileName}";
                using var stream = dto.Image2.OpenReadStream();
                imageUrl2 = await _s3.UploadFileAsync(stream, fileName, dto.Image2.ContentType);
            }
            if (dto.Image3 != null && dto.Image3.Length > 0)
            {
                var fileName = $"products/{Guid.NewGuid()}_{dto.Image3.FileName}";
                using var stream = dto.Image3.OpenReadStream();
                imageUrl3 = await _s3.UploadFileAsync(stream, fileName, dto.Image3.ContentType);
            }

            var update = new Product
            {
                Name = dto.Name,
                Description = dto.Description,
                Category = dto.Category ?? string.Empty,
                Price = dto.Price,
                BrandName = dto.BrandName,
                Sku = dto.Sku,
                Inventory = dto.Inventory,
                Available = dto.Available,
                ImageUrl1 = imageUrl1,
                ImageUrl2 = imageUrl2,
                ImageUrl3 = imageUrl3,
                CategoryId = dto.CategoryId
            };
            var result = await _products.UpdateAsync(id, update);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpDelete("{id:guid}")]
        [EnableRateLimiting("AdminWrites")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var deleted = await _products.DeleteAsync(id);
            return deleted ? NoContent() : NotFound();
        }

        // Removed upload-image endpoint; image upload is now handled in product creation

    }
}
