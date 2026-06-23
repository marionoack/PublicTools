using Microsoft.AspNetCore.Identity;
using OneTimeShare.Models;

namespace OneTimeShare.Services;

public class AdminBootstrapService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _config;
    private readonly ILogger<AdminBootstrapService> _logger;

    public AdminBootstrapService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration config,
        ILogger<AdminBootstrapService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _config = config;
        _logger = logger;
    }

    public const string AdminRole = "Admin";

    public async Task EnsureRolesAsync()
    {
        if (!await _roleManager.RoleExistsAsync(AdminRole))
            await _roleManager.CreateAsync(new IdentityRole(AdminRole));
    }

    public async Task SeedFirstAdminAsync()
    {
        var username = _config["SeedAdmin:Username"];
        var password = _config["SeedAdmin:Password"];
        var displayName = _config["SeedAdmin:DisplayName"];

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogDebug("SeedAdmin credentials not configured, skipping admin seed.");
            return;
        }

        // Only seed when no admin exists yet
        var admins = await _userManager.GetUsersInRoleAsync(AdminRole);
        if (admins.Count > 0)
        {
            _logger.LogDebug("Admin account already exists, skipping seed.");
            return;
        }

        var user = new ApplicationUser
        {
            UserName = username,
            Email = username,
            DisplayName = displayName ?? username,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            _logger.LogError("Failed to seed admin: {Errors}",
                string.Join(", ", result.Errors.Select(e => e.Description)));
            return;
        }

        await _userManager.AddToRoleAsync(user, AdminRole);
        _logger.LogInformation("Seeded first admin account: {Username}", username);
    }
}
