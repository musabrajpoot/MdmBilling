
using MdmBilling.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MdmBilling.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration config)
    {
        using var scope = services.CreateScope();

        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<AppDbContext>();

        // Apply pending database migrations
        await db.Database.MigrateAsync();

        // Seed application roles
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in AppRoles.All)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                await roles.CreateAsync(new IdentityRole(role));
            }
        }

        // Seed default server groups
        if (!await db.ServerGroups.AnyAsync())
        {
            db.ServerGroups.AddRange(
                new ServerGroup { Name = "GEPCO 1" },
                new ServerGroup { Name = "GEPCO 2" }
            );

            await db.SaveChangesAsync();
        }

        // Read initial administrator credentials from configuration
        var email = config["SeedAdmin:Email"];
        var password = config["SeedAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        // Create initial Super Admin
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();

        if (await users.FindByEmailAsync(email) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = "Super Admin",
                EmailConfirmed = true,
                IsActive = true,
                CanDownloadReports = true,
                MustChangePassword = true
            };

            var result = await users.CreateAsync(admin, password);

            if (result.Succeeded)
            {
                await users.AddToRoleAsync(admin, AppRoles.SuperAdmin);
            }
            else
            {
                var errors = string.Join(
                    "; ",
                    result.Errors.Select(e => e.Description)
                );

                throw new InvalidOperationException(
                    $"Failed to create Super Admin: {errors}");
            }
        }
    }
}
