using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OneTimeShare.Models;

namespace OneTimeShare.Pages.Profile;

public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public IndexModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [BindProperty]
    public NameInputModel NameInput { get; set; } = new();

    [BindProperty]
    public PasswordInputModel PwdInput { get; set; } = new();

    public class NameInputModel
    {
        [Required, MaxLength(100)]
        [Display(Name = "Display Name")]
        public string DisplayName { get; set; } = string.Empty;
    }

    public class PasswordInputModel
    {
        [Required]
        [Display(Name = "Current Password")]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required, MinLength(8)]
        [Display(Name = "New Password")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
        [Display(Name = "Confirm Password")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();
        NameInput.DisplayName = user.DisplayName;
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateNameAsync()
    {
        if (!ModelState.IsValid) return Page();

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        user.DisplayName = NameInput.DisplayName;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        TempData["Success"] = "Display name updated.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostChangePasswordAsync()
    {
        if (!ModelState.IsValid) return Page();

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        // Reload NameInput so the form doesn't show empty
        NameInput.DisplayName = user.DisplayName;

        var result = await _userManager.ChangePasswordAsync(user, PwdInput.CurrentPassword, PwdInput.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }

        await _signInManager.RefreshSignInAsync(user);
        TempData["Success"] = "Password changed.";
        return RedirectToPage();
    }
}
