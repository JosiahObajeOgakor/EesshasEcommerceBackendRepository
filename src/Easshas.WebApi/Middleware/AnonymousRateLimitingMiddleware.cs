using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Easshas.WebApi.Middleware
{
    /// <summary>
    /// Simple in-memory fixed-window rate limiter for anonymous requests.
    /// Configure limits with App:RateLimiting:Anonymous:PermitLimit and WindowSeconds.
    /// This is a lightweight snippet — for production use an external store (Redis) and robust library.
    /// </summary>
    public class AnonymousRateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AnonymousRateLimitingMiddleware> _logger;
        private readonly int _permitLimit;
        private readonly TimeSpan _window;

        private class WindowEntry
        {
            public int Count;
            public DateTime WindowStart;
        }

        private readonly ConcurrentDictionary<string, WindowEntry> _stores = new();

        public AnonymousRateLimitingMiddleware(RequestDelegate next, ILogger<AnonymousRateLimitingMiddleware> logger, IConfiguration config)
        {
            _next = next;
            _logger = logger;
            _permitLimit = config.GetValue<int?>("App:RateLimiting:Anonymous:PermitLimit") ?? 60;
            var seconds = config.GetValue<int?>("App:RateLimiting:Anonymous:WindowSeconds") ?? 60;
            _window = TimeSpan.FromSeconds(seconds);
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // If user is authenticated, do not apply anonymous limiter
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                await _next(context);
                return;
            }

            // Avoid limiting some known safe paths (swagger, health checks)
            var path = context.Request.Path.Value ?? string.Empty;
            if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/__routes", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var key = ip; // could also include path if desired

            var now = DateTime.UtcNow;
            var entry = _stores.GetOrAdd(key, _ => new WindowEntry { Count = 0, WindowStart = now });

            lock (entry)
            {
                if (now - entry.WindowStart > _window)
                {
                    entry.WindowStart = now;
                    entry.Count = 0;
                }

                entry.Count++;
                if (entry.Count > _permitLimit)
                {
                    // Too many requests
                    context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                    context.Response.Headers["Retry-After"] = ((int)_window.TotalSeconds).ToString();
                    _logger.LogWarning("Anonymous rate limit exceeded for {Ip} (path={Path})", ip, path);
                    return;
                }
            }

            await _next(context);
        }
    }
}
