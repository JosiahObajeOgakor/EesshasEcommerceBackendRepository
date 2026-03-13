using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;

namespace Easshas.WebApi.StartupExtensions
{
    public class ContactsAuthFilter : IAsyncActionFilter
    {
        private readonly IConfiguration _config;
        public ContactsAuthFilter(IConfiguration config)
        {
            _config = config;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var req = context.HttpContext.Request;
            var (ok, reason) = Validate(req);
            if (!ok)
            {
                context.HttpContext.Response.Headers["WWW-Authenticate"] = "Basic realm=contacts";
                context.Result = new UnauthorizedObjectResult(new { message = reason ?? "Unauthorized" });
                return;
            }
            await next();
        }

        private (bool, string?) Validate(HttpRequest req)
        {
            var expectedUser = _config["Contacts:Auth:Username"] ?? string.Empty;
            var expectedPass = _config["Contacts:Auth:Password"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(expectedUser) || string.IsNullOrWhiteSpace(expectedPass))
            {
                return (false, "Contacts passkey not configured");
            }

            // 1) Authorization: Basic base64(username:password)
            var auth = req.Headers["Authorization"].ToString();
            if (!string.IsNullOrWhiteSpace(auth) && auth.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                var b64 = auth.Substring("Basic ".Length).Trim();
                try
                {
                    var dec = Encoding.UTF8.GetString(Convert.FromBase64String(b64));
                    var idx = dec.IndexOf(':');
                    if (idx > 0)
                    {
                        var user = dec.Substring(0, idx);
                        var pass = dec.Substring(idx + 1);
                        if (SafeEquals(user, expectedUser) && SafeEquals(pass, expectedPass))
                        {
                            return (true, null);
                        }
                    }
                }
                catch { /* fall through */ }
            }

            // 2) Custom headers
            var hUser = req.Headers["X-Contacts-User"].ToString();
            var hPass = req.Headers["X-Contacts-Pass"].ToString();
            if (!string.IsNullOrWhiteSpace(hUser) && !string.IsNullOrWhiteSpace(hPass))
            {
                if (SafeEquals(hUser, expectedUser) && SafeEquals(hPass, expectedPass))
                {
                    return (true, null);
                }
            }

            // 3) Query params
            var qUser = req.Query["user"].ToString();
            var qPass = req.Query["pass"].ToString();
            if (!string.IsNullOrWhiteSpace(qUser) && !string.IsNullOrWhiteSpace(qPass))
            {
                if (SafeEquals(qUser, expectedUser) && SafeEquals(qPass, expectedPass))
                {
                    return (true, null);
                }
            }

            return (false, "Missing or invalid passkey");
        }

        private static bool SafeEquals(string a, string b)
        {
            // constant-time-ish comparison
            if (a.Length != b.Length) return false;
            var diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }
    }
}
