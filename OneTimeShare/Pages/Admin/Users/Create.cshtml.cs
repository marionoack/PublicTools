using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OneTimeShare.Models;
using OneTimeShare.Services;

namespace OneTimeShare.Pages.Admin.Users;

public class CreateModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public CreateModel(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required, MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        [Display(Name = "Display Name")]
        public string DisplayName { get; set; } = string.Empty;

        [Required, MinLength(8)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Administrator")]
        public bool IsAdmin { get; set; }
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var user = new ApplicationUser
        {
            UserName = Input.Username,
            Email = Input.Username,
            DisplayName = Input.DisplayName,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }

        if (Input.IsAdmin)
            await _userManager.AddToRoleAsync(user, AdminBootstrapService.AdminRole);

        TempData["Success"] = $"User '{Input.Username}' created.";
        return RedirectToPage("/Admin/Users/Index");
    }
}
