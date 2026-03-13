using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Easshas.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Easshas.WebApi.Controllers
{
    [ApiController]
    [Route("api/contacts")]
    [ServiceFilter(typeof(Easshas.WebApi.StartupExtensions.ContactsAuthFilter))]
    public class ContactsController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IContactMessageService _messageService;
        
        public ContactsController(IConfiguration configuration, IContactMessageService messageService)
        {
            _configuration = configuration;
            _messageService = messageService;
        }

        public record PhoneDto(string Phone);
        public record EmailDto(string Email);
        public record ContactDto(string? Phone, string? Email);
        
        public record MessageSubmissionDto(
            [Required(ErrorMessage = "Name is required")]
            [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters")]
            string Name,
            
            [Required(ErrorMessage = "Message is required")]
            [StringLength(5000, MinimumLength = 10, ErrorMessage = "Message must be between 10 and 5000 characters")]
            string Message,
            
            [Required(ErrorMessage = "Phone number is required")]
            string PhoneNumber
        );

        private static string CleanPhone(string phone)
        {
            var cleaned = (phone ?? string.Empty).Trim();
            cleaned = cleaned.Replace(" ", string.Empty).Replace("-", string.Empty).Replace("(", string.Empty).Replace(")", string.Empty);
            return cleaned;
        }

        private static bool IsValidE164(string cleaned)
        {
            // E.164: +[country][number], 8-15 digits, no leading 0 after country code
            return Regex.IsMatch(cleaned, "^\\+?[1-9][0-9]{7,14}$");
        }

        private static bool IsValidEmailFormat(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private string[] GetRecognizedProviders()
        {
            var fromConfig = _configuration.GetSection("Email:RecognizedProviders").Get<string[]>() ?? Array.Empty<string>();
            if (fromConfig.Length > 0) return fromConfig.Select(d => d.Trim().ToLowerInvariant()).ToArray();
            return new[]
            {
                "gmail.com","yahoo.com","outlook.com","hotmail.com","live.com","zoho.com","protonmail.com","icloud.com","yandex.com"
            };
        }

        private bool IsRecognizedProvider(string email)
        {
            var at = email.LastIndexOf('@');
            if (at < 0) return false;
            var domain = email[(at + 1)..].Trim().ToLowerInvariant();
            var list = GetRecognizedProviders();
            return list.Contains(domain);
        }

        

        // Unified single endpoint handling phone OR email
        [HttpPost]
        [Route("")] // /api/contacts
        [AllowAnonymous]
        public IActionResult Create([FromBody] ContactDto dto)
        {
            var hasPhone = !string.IsNullOrWhiteSpace(dto.Phone);
            var hasEmail = !string.IsNullOrWhiteSpace(dto.Email);
            if (hasPhone == hasEmail)
            {
                return BadRequest(new { message = "Provide exactly one of 'phone' or 'email'." });
            }

            if (hasPhone)
            {
                var cleaned = CleanPhone(dto.Phone!);
                if (!IsValidE164(cleaned))
                {
                    return BadRequest(new { message = "Invalid phone number. Provide E.164 format (e.g., +2348012345678)." });
                }
                return Created($"/api/contacts?phone={Uri.EscapeDataString(cleaned)}", new { type = "phone", phone = cleaned, valid = true });
            }
            else
            {
                var email = dto.Email!.Trim();
                if (!IsValidEmailFormat(email) || !IsRecognizedProvider(email))
                {
                    return BadRequest(new { message = "Invalid email or unrecognized provider.", recognizedProviders = GetRecognizedProviders() });
                }
                var at = email.LastIndexOf('@');
                var domain = email[(at + 1)..].Trim().ToLowerInvariant();
                return Created($"/api/contacts?email={Uri.EscapeDataString(email)}", new { type = "email", email, provider = domain, valid = true });
            }
        }

        [HttpGet]
        [Route("")] // /api/contacts
        [AllowAnonymous]
        public IActionResult Get([FromQuery] string? phone, [FromQuery] string? email)
        {
            var hasPhone = !string.IsNullOrWhiteSpace(phone);
            var hasEmail = !string.IsNullOrWhiteSpace(email);
            if (hasPhone == hasEmail)
            {
                return BadRequest(new { message = "Provide exactly one query param: 'phone' or 'email'." });
            }

            if (hasPhone)
            {
                var cleaned = CleanPhone(phone!);
                var valid = IsValidE164(cleaned);
                return Ok(new { type = "phone", phone = cleaned, valid });
            }
            else
            {
                var e = email!.Trim();
                var validFormat = IsValidEmailFormat(e);
                var recognized = validFormat && IsRecognizedProvider(e);
                var at = e.LastIndexOf('@');
                var domain = (at >= 0) ? e[(at + 1)..].Trim().ToLowerInvariant() : string.Empty;
                return Ok(new { type = "email", email = e, validFormat, recognizedProvider = recognized, provider = domain });
            }
        }

        [HttpPatch]
        [Route("")] // /api/contacts
        [AllowAnonymous]
        public IActionResult Patch([FromBody] ContactDto dto)
        {
            var hasPhone = !string.IsNullOrWhiteSpace(dto.Phone);
            var hasEmail = !string.IsNullOrWhiteSpace(dto.Email);
            if (hasPhone == hasEmail)
            {
                return BadRequest(new { message = "Provide exactly one of 'phone' or 'email'." });
            }

            if (hasPhone)
            {
                var cleaned = CleanPhone(dto.Phone!);
                if (!IsValidE164(cleaned))
                {
                    return BadRequest(new { message = "Invalid phone number." });
                }
                return Ok(new { type = "phone", phone = cleaned, valid = true });
            }
            else
            {
                var e = dto.Email!.Trim();
                if (!IsValidEmailFormat(e) || !IsRecognizedProvider(e))
                {
                    return BadRequest(new { message = "Invalid email or unrecognized provider.", recognizedProviders = GetRecognizedProviders() });
                }
                var at = e.LastIndexOf('@');
                var domain = e[(at + 1)..].Trim().ToLowerInvariant();
                return Ok(new { type = "email", email = e, provider = domain, valid = true });
            }
        }

        [HttpDelete]
        [Route("")] // /api/contacts
        [AllowAnonymous]
        public IActionResult Delete([FromQuery] string? phone, [FromQuery] string? email)
        {
            var hasPhone = !string.IsNullOrWhiteSpace(phone);
            var hasEmail = !string.IsNullOrWhiteSpace(email);
            if (hasPhone == hasEmail)
            {
                return BadRequest(new { message = "Provide exactly one query param: 'phone' or 'email'." });
            }

            if (hasPhone)
            {
                var cleaned = CleanPhone(phone!);
                if (!IsValidE164(cleaned))
                {
                    return BadRequest(new { message = "Invalid phone number." });
                }
                return NoContent();
            }
            else
            {
                var e = (email ?? string.Empty).Trim();
                if (!IsValidEmailFormat(e) || !IsRecognizedProvider(e))
                {
                    return BadRequest(new { message = "Invalid email or unrecognized provider." });
                }
                return NoContent();
            }
        }

        // Message submission endpoints
        [HttpPost]
        [Route("messages")] // /api/contacts/messages
        [AllowAnonymous]
        public async Task<IActionResult> SubmitMessage([FromBody] MessageSubmissionDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var cleaned = CleanPhone(dto.PhoneNumber);
            if (!IsValidE164(cleaned))
            {
                return BadRequest(new { message = "Invalid phone number. Provide E.164 format (e.g., +2348012345678)." });
            }

            var submission = new ContactMessage
            {
                Name = dto.Name.Trim(),
                Message = dto.Message.Trim(),
                PhoneNumber = cleaned
            };

            var created = await _messageService.CreateAsync(submission);

            return CreatedAtAction(nameof(GetMessage), new { id = created.Id }, created);
        }

        [HttpGet]
        [Route("messages")] // /api/contacts/messages
        [AllowAnonymous]
        public async Task<IActionResult> GetMessages([FromQuery] int skip = 0, [FromQuery] int take = 50)
        {
            if (skip < 0 || take < 1 || take > 100)
            {
                return BadRequest(new { message = "Invalid pagination parameters. skip >= 0, take between 1 and 100." });
            }

            var messages = await _messageService.ListAsync(skip, take);
            var total = await _messageService.GetCountAsync();

            return Ok(new { total, skip, take, messages });
        }

        [HttpGet]
        [Route("messages/{id:guid}")] // /api/contacts/messages/{id}
        [AllowAnonymous]
        public async Task<IActionResult> GetMessage(Guid id)
        {
            var message = await _messageService.GetByIdAsync(id);
            return message == null ? NotFound() : Ok(message);
        }
    }
}
