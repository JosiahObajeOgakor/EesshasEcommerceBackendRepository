using System;
using System.Threading.Tasks;
using Easshas.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Easshas.WebApi.StartupExtensions
{
    public static class DatabaseSeeder
    {
        public static async Task SeedAsync(this IServiceProvider services)
        {
            try
            {
                using var scope = services.CreateScope();
                var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                var seedFlag = config.GetValue<bool?>("App:SeedOnStartup");
                if (seedFlag == false)
                {
                    Console.WriteLine("[Seeder] SeedOnStartup=false, skipping seeding.");
                    return;
                }

                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

                var userRole = "User";
                var adminRole = "Admin";
                if (!await roleManager.RoleExistsAsync(adminRole))
                {
                    await roleManager.CreateAsync(new IdentityRole<Guid>(adminRole));
                }
                if (!await roleManager.RoleExistsAsync(userRole))
                {
                    await roleManager.CreateAsync(new IdentityRole<Guid>(userRole));
                }

                var adminUser = config["Admin:Username"];
                var adminPass = config["Admin:Password"];
                var adminEmail = config["Admin:Email"] ?? "admin@example.com";
                if (!string.IsNullOrWhiteSpace(adminUser) && !string.IsNullOrWhiteSpace(adminPass))
                {
                    var user = await userManager.FindByNameAsync(adminUser);
                    if (user == null)
                    {
                        user = new ApplicationUser { UserName = adminUser, Email = adminEmail, EmailConfirmed = true };
                        var result = await userManager.CreateAsync(user, adminPass);
                        if (result.Succeeded)
                        {
                            await userManager.AddToRoleAsync(user, adminRole);
                        }
                    }
                    else
                    {
                        if (!await userManager.IsInRoleAsync(user, adminRole))
                        {
                            await userManager.AddToRoleAsync(user, adminRole);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Seeder] Skipping startup seeding: {ex.Message}");
            }
        }
    }
}
