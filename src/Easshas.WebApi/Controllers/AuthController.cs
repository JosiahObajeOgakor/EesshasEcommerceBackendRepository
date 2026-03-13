using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Easshas.Infrastructure.Configuration;
using Easshas.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Easshas.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IJwtTokenService _jwtService;
        private readonly JwtOptions _jwtOptions;
        private readonly IRefreshTokenService _refreshService;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;

        public AuthController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IJwtTokenService jwtService, IOptions<JwtOptions> jwtOptions, IRefreshTokenService refreshService, RoleManager<IdentityRole<Guid>> roleManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtService = jwtService;
            _jwtOptions = jwtOptions.Value;
            _refreshService = refreshService;
            _roleManager = roleManager;
        }

        public record SignupRequest(string FirstName, string LastName, string Email, string PhoneNumber, string Password);
        public record SigninRequest(string Username, string Password);

        [HttpPost("signup")]
        [AllowAnonymous]
        [EnableRateLimiting("AuthSignin")]
        public async Task<IActionResult> Signup([FromBody] SignupRequest req)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(req.FirstName) || string.IsNullOrWhiteSpace(req.LastName))
            {
                return BadRequest(new { message = "First name and last name are required." });
            }
            if (string.IsNullOrWhiteSpace(req.Email))
            {
                return BadRequest(new { message = "Email is required." });
            }
            if (string.IsNullOrWhiteSpace(req.PhoneNumber))
            {
                return BadRequest(new { message = "Phone number is required." });
            }
            if (string.IsNullOrWhiteSpace(req.Password))
            {
                return BadRequest(new { message = "Password is required." });
            }

            // Check if email already exists
            var existingByEmail = await _userManager.FindByEmailAsync(req.Email);
            if (existingByEmail != null)
            {
                return Conflict(new { message = "Email already exists." });
            }

            // Use email as username
            var username = req.Email;
            var existingByName = await _userManager.FindByNameAsync(username);
            if (existingByName != null)
            {
                return Conflict(new { message = "Email already exists." });
            }

            var user = new ApplicationUser
            {
                UserName = username,
                Email = req.Email,
                PhoneNumber = req.PhoneNumber,
                FirstName = req.FirstName.Trim(),
                LastName = req.LastName.Trim(),
                FullName = $"{req.FirstName.Trim()} {req.LastName.Trim()}"
            };

            var result = await _userManager.CreateAsync(user, req.Password);
            if (!result.Succeeded)
            {
                return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
            }

            // Ensure default role exists and assign 'User' role
            var defaultRole = "User";
            if (!await _roleManager.RoleExistsAsync(defaultRole))
            {
                await _roleManager.CreateAsync(new IdentityRole<Guid>(defaultRole));
            }
            await _userManager.AddToRoleAsync(user, defaultRole);

            return Ok(new
            {
                message = "Signup successful",
                user = new
                {
                    id = user.Id,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    email = user.Email,
                    phoneNumber = user.PhoneNumber,
                    fullName = user.FullName
                }
            });
        }

        // Generic signin removed to enforce separate admin/user login flows.

        [HttpPost("signin/admin")]
        [AllowAnonymous]
        [EnableRateLimiting("AuthSignin")]
        public async Task<IActionResult> SigninAdmin([FromBody] SigninRequest req)
        {
            var user = await _userManager.FindByNameAsync(req.Username);
            if (user == null) return Unauthorized();

            var passOk = await _signInManager.CheckPasswordSignInAsync(user, req.Password, true);
            if (!passOk.Succeeded) return Unauthorized();

            var roles = await _userManager.GetRolesAsync(user);
            var isAdmin = roles.Contains("Admin");
            if (!isAdmin)
            {
                return Forbid();
            }

            var token = await _jwtService.GenerateTokenAsync(user.Id.ToString(), user.UserName!, roles);

            Response.Cookies.Append(_jwtOptions.CookieName, token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/",
                IsEssential = true,
                Expires = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes)
            });

            var rt = await _refreshService.CreateAsync(Guid.Parse(user.Id.ToString()), _jwtOptions.RefreshExpiryMinutes);
            Response.Cookies.Append(_jwtOptions.RefreshCookieName, rt, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/",
                IsEssential = true,
                Expires = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.RefreshExpiryMinutes)
            });

            return Ok(new { message = "Admin signin successful", token, roles });
        }

        [HttpPost("signin/user")]
        [AllowAnonymous]
        [EnableRateLimiting("AuthSignin")]
        public async Task<IActionResult> SigninUser([FromBody] SigninRequest req)
        {
            var user = await _userManager.FindByNameAsync(req.Username);
            if (user == null) return Unauthorized();

            var passOk = await _signInManager.CheckPasswordSignInAsync(user, req.Password, true);
            if (!passOk.Succeeded) return Unauthorized();

            var roles = await _userManager.GetRolesAsync(user);
            var isUser = roles.Contains("User");
            if (!isUser)
            {
                return Forbid();
            }

            var token = await _jwtService.GenerateTokenAsync(user.Id.ToString(), user.UserName!, roles);

            Response.Cookies.Append(_jwtOptions.CookieName, token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/",
                IsEssential = true,
                Expires = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes)
            });

            var rt = await _refreshService.CreateAsync(Guid.Parse(user.Id.ToString()), _jwtOptions.RefreshExpiryMinutes);
            Response.Cookies.Append(_jwtOptions.RefreshCookieName, rt, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/",
                IsEssential = true,
                Expires = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.RefreshExpiryMinutes)
            });

            return Ok(new
            {
                message = "User signin successful",
                token,
                user = new
                {
                    id = user.Id,
                    username = user.UserName,
                    email = user.Email,
                    name = user.FullName,
                    phone = user.PhoneNumber,
                    roles
                }
            });
        }

        [HttpPost("signout")]
        [EnableRateLimiting("AuthSignin")]
        public IActionResult Signout()
        {
            // Revoke refresh token server-side if present
            if (Request.Cookies.TryGetValue(_jwtOptions.RefreshCookieName, out var refreshToken) && !string.IsNullOrWhiteSpace(refreshToken))
            {
                try
                {
                    _refreshService.RevokeAsync(refreshToken).GetAwaiter().GetResult();
                }
                catch
                {
                    // ignore revoke errors - still proceed to clear cookies
                }
            }

            // Delete both access and refresh cookies
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/"
            };
            Response.Cookies.Delete(_jwtOptions.CookieName, cookieOptions);
            Response.Cookies.Delete(_jwtOptions.RefreshCookieName, cookieOptions);

            return Ok(new { message = "Signed out" });
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Unauthorized();
            var roles = await _userManager.GetRolesAsync(user);
            var isAdmin = roles.Contains("Admin");
            return Ok(new
            {
                isAdmin,
                user = new
                {
                    id = user.Id,
                    username = user.UserName,
                    email = user.Email,
                    name = user.FullName,
                    phone = user.PhoneNumber,
                    roles
                }
            });
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        [EnableRateLimiting("AuthRefresh")]
        public async Task<IActionResult> Refresh()
        {
            if (!Request.Cookies.TryGetValue(_jwtOptions.RefreshCookieName, out var tokenValue) || string.IsNullOrWhiteSpace(tokenValue))
            {
                return Unauthorized(new { message = "Missing refresh token." });
            }
            try
            {
                var (newAccess, newRefresh) = await _refreshService.ValidateAndRotateAsync(tokenValue, _jwtOptions.ExpiryMinutes, _jwtOptions.RefreshExpiryMinutes);
                // Set new access token cookie
                Response.Cookies.Append(_jwtOptions.CookieName, newAccess, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    Path = "/",
                    IsEssential = true,
                    Expires = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes)
                });
                // Set new refresh token cookie
                Response.Cookies.Append(_jwtOptions.RefreshCookieName, newRefresh, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    Path = "/",
                    IsEssential = true,
                    Expires = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.RefreshExpiryMinutes)
                });

                return Ok(new { token = newAccess });
            }
            catch (Exception ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }
    }
}
