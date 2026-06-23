using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OneTimeShare.Models;
using OneTimeShare.Services;

namespace OneTimeShare.Pages.Admin.Users;

public class UserViewModel
{
    public string Id { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public bool IsDisabled { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public IList<UserViewModel> Users { get; set; } = [];
    public string? CurrentUserId { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        CurrentUserId = _userManager.GetUserId(User);
        var all = _userManager.Users.OrderBy(u => u.UserName).ToList();
        var vms = new List<UserViewModel>();
        foreach (var u in all)
        {
            var roles = await _userManager.GetRolesAsync(u);
            vms.Add(new UserViewModel
            {
                Id = u.Id,
                UserName = u.UserName,
                DisplayName = u.DisplayName,
                IsAdmin = roles.Contains(AdminBootstrapService.AdminRole),
                IsDisabled = u.IsDisabled,
                CreatedAt = u.CreatedAt
            });
        }
        Users = vms;
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        var currentUserId = _userManager.GetUserId(User);
        if (id == currentUserId) return BadRequest("Cannot delete yourself.");

        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        await _userManager.DeleteAsync(user);
        TempData["Success"] = $"User '{user.UserName}' deleted.";
        return RedirectToPage();
    }
}
