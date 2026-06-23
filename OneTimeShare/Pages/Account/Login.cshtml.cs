using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OneTimeShare.Models;
using OneTimeShare.Services;

namespace OneTimeShare.Pages.Account;

public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _audit;

    public LoginModel(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IAuditService audit)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _audit = audit;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToPage("/Dashboard/Index");

        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (!ModelState.IsValid) return Page();

        var user = await _userManager.FindByNameAsync(Input.Username);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        if (user is null || user.IsDisabled)
        {
            await _audit.LogAsync(AuditEventType.LoginFailed, false,
                username: Input.Username, details: user?.IsDisabled == true ? "Account disabled" : "User not found", ipAddress: ip);
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return Page();
        }

        var result = await _signInManager.PasswordSignInAsync(user, Input.Password, Input.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            await _audit.LogAsync(AuditEventType.Login, true,
                userId: user.Id, username: user.UserName, ipAddress: ip);
            return LocalRedirect(returnUrl ?? "/Dashboard");
        }

        await _audit.LogAsync(AuditEventType.LoginFailed, false,
            userId: user.Id, username: user.UserName,
            details: result.IsLockedOut ? "Locked out" : "Wrong password", ipAddress: ip);

        ModelState.AddModelError(string.Empty,
            result.IsLockedOut ? "Account locked. Try again later." : "Invalid username or password.");
        return Page();
    }
}
