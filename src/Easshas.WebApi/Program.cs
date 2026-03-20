using System;
using System.Text;
using Easshas.Infrastructure;
using Easshas.Infrastructure.Configuration;
using Easshas.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Easshas.WebApi.StartupExtensions;
using Easshas.WebApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Harden Kestrel against abuse
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
    serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
    serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(60);
});

// Add services to the container.
builder.Services.AddInfrastructure(builder.Configuration);

// Load Jwt options early for Swagger and auth configuration
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();

// Bind URLs preferring env var, then launch profile (applicationUrl), else defaults
var urlsEnv = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
var urlsConfig = builder.Configuration["urls"]; // populated by launchSettings applicationUrl
if (!string.IsNullOrWhiteSpace(urlsEnv))
{
    builder.WebHost.UseUrls(urlsEnv.Split(';'));
}
else if (!string.IsNullOrWhiteSpace(urlsConfig))
{
    builder.WebHost.UseUrls(urlsConfig.Split(';'));
}
    else
    {
        // Prefer IPv4 loopback and also bind localhost so browser 'localhost' and '127.0.0.1' both work
        builder.WebHost.UseUrls("https://127.0.0.1:7106", "http://127.0.0.1:5272", "https://localhost:7106", "http://localhost:5272");
    }

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<Easshas.Infrastructure.Persistence.AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Easshas API",
        Version = "v1",
        Description = "Use POST /api/auth/signin/user for user login or /api/auth/signin/admin for admin login. Then click Authorize and choose the Bearer scheme to paste your JWT."
    });
    var jwtSecurityScheme = new OpenApiSecurityScheme
    {
        Scheme = "bearer",
        BearerFormat = "JWT",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Description = "Paste your JWT token here",
        Reference = new OpenApiReference
        {
            Id = "Bearer",
            Type = ReferenceType.SecurityScheme
        }
    };

    c.AddSecurityDefinition("Bearer", jwtSecurityScheme);

    // Basic scheme for Contacts passkey
    var contactsBasicScheme = new OpenApiSecurityScheme
    {
        Scheme = "basic",
        Type = SecuritySchemeType.Http,
        Description = "Contacts passkey (Basic auth). Use configured username/password.",
        Reference = new OpenApiReference
        {
            Id = "ContactsBasic",
            Type = ReferenceType.SecurityScheme
        }
    };
    c.AddSecurityDefinition("ContactsBasic", contactsBasicScheme);

    // Add per-endpoint security: only require auth when [Authorize] is present
    // Updated: attach Bearer to non-signin endpoints, Basic to /api/contacts
    c.OperationFilter<SwaggerAuthOperationFilter>();
});

builder.Services.AddSignalR();
builder.Services.AddHttpClient();
// In-memory cache for short-lived counters (OTP rate-limits)
builder.Services.AddMemoryCache();
builder.Services.AddScoped<Easshas.WebApi.StartupExtensions.ContactsAuthFilter>();
builder.Services.AddScoped<Easshas.Infrastructure.Services.S3Service>();
builder.Services.AddHostedService<Easshas.WebApi.Services.PaymentReconciliationService>();
builder.Services.AddHostedService<Easshas.WebApi.Services.PendingOrderCleanupService>();

// Explicit HTTPS redirection port to avoid 'Failed to determine https port'
builder.Services.AddHttpsRedirection(options =>
{
    options.HttpsPort = 7106;
});

// Rate limiting: global + specific policies
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Global limiter per IP (basic DDoS protection for anonymous traffic)
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var limit = builder.Configuration.GetValue<int?>("App:RateLimiting:Global:PermitLimit") ?? 200;
        var window = builder.Configuration.GetValue<int?>("App:RateLimiting:Global:WindowSeconds") ?? 60;
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit,
            Window = TimeSpan.FromSeconds(window),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    // Admin write operations (stronger limits)
    options.AddPolicy("AdminWrites", httpContext =>
    {
        var userId = httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var key = string.IsNullOrWhiteSpace(userId) ? (httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous") : userId;

        var permitLimit = builder.Configuration.GetValue<int?>("App:RateLimiting:AdminWrites:PermitLimit") ?? 10;
        var windowSeconds = builder.Configuration.GetValue<int?>("App:RateLimiting:AdminWrites:WindowSeconds") ?? 60;

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey: key, factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromSeconds(windowSeconds),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    // Auth signin policy (prevent brute force)
    options.AddPolicy("AuthSignin", httpContext =>
    {
        var idOrIp = httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";
        var limit = builder.Configuration.GetValue<int?>("App:RateLimiting:AuthSignin:PermitLimit") ?? 5;
        var window = builder.Configuration.GetValue<int?>("App:RateLimiting:AuthSignin:WindowSeconds") ?? 60;
        return RateLimitPartition.GetFixedWindowLimiter(idOrIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit,
            Window = TimeSpan.FromSeconds(window),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    // Refresh token policy (prevent abuse)
    options.AddPolicy("AuthRefresh", httpContext =>
    {
        var idOrIp = httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";
        var limit = builder.Configuration.GetValue<int?>("App:RateLimiting:AuthRefresh:PermitLimit") ?? 20;
        var window = builder.Configuration.GetValue<int?>("App:RateLimiting:AuthRefresh:WindowSeconds") ?? 60;
        return RateLimitPartition.GetFixedWindowLimiter(idOrIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit,
            Window = TimeSpan.FromSeconds(window),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
});

// CORS configuration - whitelist origins with credentials support
// Ensures secure cross-origin requests for both development and production
builder.Services.AddCors(options =>
{
    // Load primary origins from config: localhost:3000 and https://eeshasgloss.com
    var configured = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? new[] 
    { 
        "http://localhost:3000", 
        "https://localhost:3000", 
        "https://eeshasgloss.com"
    };
    var origins = new System.Collections.Generic.List<string>(configured);
    
    // Add development helper origins (127.0.0.1 variants and localhost:5272 for API testing)
    void TryAdd(string u) 
    { 
        if (!string.IsNullOrWhiteSpace(u) && !origins.Contains(u)) 
            origins.Add(u); 
    }
    
    if (builder.Environment.IsDevelopment())
    {
        TryAdd("http://127.0.0.1:3000");
        TryAdd("https://127.0.0.1:3000");
        TryAdd("http://localhost:5272");
        TryAdd("https://localhost:5272");
        TryAdd("http://127.0.0.1:5272");
        TryAdd("https://127.0.0.1:7106");
        TryAdd("https://localhost:7106");
    }

    options.AddPolicy("Default", policy =>
    {
        policy
            .WithOrigins(origins.ToArray())
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials() // Essential for cookie-based auth
            .WithExposedHeaders("Content-Disposition", "X-Total-Count");
    });
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ClockSkew = TimeSpan.Zero,
            NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                // Prefer Authorization header if present; fall back to HttpOnly cookie
                var authHeader = ctx.Request.Headers["Authorization"].ToString();
                if (string.IsNullOrWhiteSpace(authHeader))
                {
                    if (ctx.Request.Cookies.TryGetValue(jwtOptions.CookieName, out var token))
                    {
                        ctx.Token = token;
                    }
                }
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = ctx =>
            {
                Console.WriteLine($"JWT auth failed: {ctx.Exception.Message}");
                return Task.CompletedTask;
            },
            OnChallenge = ctx =>
            {
                // Log why a 401 was issued for easier debugging
                Console.WriteLine("JWT challenge issued (401). Token missing/invalid.");
                return Task.CompletedTask;
            }
        };
    });

// Centralized authorization policies for cleaner role checks
builder.Services.AddAuthorization(options =>
{
    // Remove global default auth requirement; endpoints are open unless annotated.
    // Policies remain defined for optional future use.
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("UserOnly", policy => policy.RequireRole("User"));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

if (app.Environment.IsDevelopment())
{
    // Lightweight route inspection to debug 404s in dev
    app.MapGet("/__routes", (IEnumerable<Microsoft.AspNetCore.Routing.EndpointDataSource> sources) =>
    {
        var endpoints = sources
            .SelectMany(s => s.Endpoints)
            .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Select(e => new
            {
                Route = e.RoutePattern.RawText,
                Methods = e.Metadata
                    .OfType<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()
                    .SelectMany(m => m.HttpMethods)
                    .Distinct()
                    .ToArray(),
                e.DisplayName
            });
        return Results.Json(endpoints);
    });

    // Log requests and matched endpoint for debugging in development
    app.Use(async (ctx, next) =>
    {
        var epBefore = ctx.GetEndpoint()?.DisplayName ?? "(no endpoint yet)";
        Console.WriteLine($"REQ {ctx.Request.Method} {ctx.Request.Path} -> {epBefore}");
        await next();
        var epAfter = ctx.GetEndpoint()?.DisplayName ?? "(no endpoint)";
        Console.WriteLine($"RES {ctx.Response.StatusCode} {ctx.Request.Method} {ctx.Request.Path} -> {epAfter}");
    });
}

// Enforce cookie policy for secure credentials
// In development, allow non-secure cookies for localhost testing
// In production, always use secure cookies
app.UseCookiePolicy(new CookiePolicyOptions
{
    Secure = app.Environment.IsProduction() ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest,
    MinimumSameSitePolicy = SameSiteMode.None
});
// app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("Default");

// Lightweight anonymous-only limiter middleware (in-memory). This applies before authentication.
app.UseMiddleware<AnonymousRateLimitingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHub<Easshas.Infrastructure.RealTime.TrackingHub>("/hubs/tracking");

// Seed roles/admin if configured and enabled
var seedEnabled = builder.Configuration.GetValue<bool?>("App:SeedOnStartup") ?? true;
if (seedEnabled)
{
    await Easshas.WebApi.StartupExtensions.DatabaseSeeder.SeedAsync(app.Services);
}

app.Run();
