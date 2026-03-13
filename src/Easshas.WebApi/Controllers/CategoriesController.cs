using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Easshas.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
namespace Easshas.WebApi.Controllers
{
    [ApiController]
    [Route("api/admin/categories")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "User,Admin")]
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryService _categories;
        public CategoriesController(ICategoryService categories) { _categories = categories; }

        public class CreateCategoryDto
        {
            [Required]
            [StringLength(100, MinimumLength = 2)]
            public string Name { get; set; } = string.Empty;

            [Required]
            [StringLength(150, MinimumLength = 2)]
            public string Slug { get; set; } = string.Empty;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCategoryDto dto)
        {
            var category = new Category { Name = dto.Name, Slug = dto.Slug };
            try
            {
                var created = await _categories.CreateAsync(category);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(Guid id)
        {
            var c = await _categories.GetByIdAsync(id);
            return c is null ? NotFound() : Ok(c);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> List()
        {
            var list = await _categories.ListAsync(true);
            return Ok(list);
        }
    }
}
