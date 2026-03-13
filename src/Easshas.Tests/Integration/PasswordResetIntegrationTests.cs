using System;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Easshas.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Easshas.Tests.Integration
{
    public class PasswordResetIntegrationTests : IClassFixture<CustomFactory>
    {
        private readonly CustomFactory _factory;

        public PasswordResetIntegrationTests(CustomFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task RequestAndConfirmOtp_Works()
        {
            var client = _factory.CreateClient();

            var email = "testuser@example.com";

            var req = new { Email = email };
            var resp = await client.PostAsJsonAsync("/api/auth/password/request", req);
            resp.EnsureSuccessStatusCode();

            // Read OTP from DB
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var otpEntry = await db.PasswordResetOtps.FirstOrDefaultAsync(p => p.Email == email);
            Assert.NotNull(otpEntry);

            var confirm = new { Email = email, Otp = otpEntry.Otp, NewPassword = "NewPass@123" };
            var cResp = await client.PostAsJsonAsync("/api/auth/password/confirm", confirm);
            cResp.EnsureSuccessStatusCode();

            var usedEntry = await db.PasswordResetOtps.FirstOrDefaultAsync(p => p.Email == email && p.Otp == otpEntry.Otp);
            Assert.True(usedEntry.Used);
        }
    }
}
