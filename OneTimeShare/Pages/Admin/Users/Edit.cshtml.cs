using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OneTimeShare.Models;
using OneTimeShare.Services;

namespace OneTimeShare.Pages.Admin.Users;

public class EditModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public EditModel(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public ResetPasswordModel ResetPwd { get; set; } = new();

    public bool IsSelf { get; private set; }

    public class InputModel
    {
        public string Id { get; set; } = string.Empty;
        public string? Username { get; set; }

        [Required, MaxLength(100)]
        [Display(Name = "Display Name")]
        public string DisplayName { get; set; } = string.Empty;

        [Display(Name = "Administrator")]
        public bool IsAdmin { get; set; }

        [Display(Name = "Disabled")]
        public bool IsDisabled { get; set; }
    }

    public class ResetPasswordModel
    {
        [Required, MinLength(8)]
        [Display(Name = "New Password")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        IsSelf = _userManager.GetUserId(User) == id;
        var roles = await _userManager.GetRolesAsync(user);
        Input = new InputModel
        {
            Id = user.Id,
            Username = user.UserName,
            DisplayName = user.DisplayName,
            IsAdmin = roles.Contains(AdminBootstrapService.AdminRole),
            IsDisabled = user.IsDisabled
        };
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (!ModelState.IsValid) return Page();

        var user = await _userManager.FindByIdAsync(Input.Id);
        if (user is null) return NotFound();

        IsSelf = _userManager.GetUserId(User) == Input.Id;
        user.DisplayName = Input.DisplayName;
        user.UpdatedAt = DateTime.UtcNow;

        if (!IsSelf)
        {
            user.IsDisabled = Input.IsDisabled;
            var isCurrentlyAdmin = await _userManager.IsInRoleAsync(user, AdminBootstrapService.AdminRole);
            if (Input.IsAdmin && !isCurrentlyAdmin)
                await _userManager.AddToRoleAsync(user, AdminBootstrapService.AdminRole);
            else if (!Input.IsAdmin && isCurrentlyAdmin)
                await _userManager.RemoveFromRoleAsync(user, AdminBootstrapService.AdminRole);
        }

        await _userManager.UpdateAsync(user);
        TempData["Success"] = $"User '{user.UserName}' updated.";
        return RedirectToPage("/Admin/Users/Index");
    }

    public async Task<IActionResult> OnPostResetPasswordAsync()
    {
        if (!ModelState.IsValid)
        {
            // Reload user info for the form
            var u = await _userManager.FindByIdAsync(Input.Id);
            if (u is not null) Input.Username = u.UserName;
            return Page();
        }

        var user = await _userManager.FindByIdAsync(Input.Id);
        if (user is null) return NotFound();

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, ResetPwd.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            Input.Username = user.UserName;
            return Page();
        }

        TempData["Success"] = $"Password for '{user.UserName}' reset.";
        return RedirectToPage("/Admin/Users/Index");
    }
}
