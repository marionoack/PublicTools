using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OneTimeShare.Data;
using OneTimeShare.Models;
using OneTimeShare.Services;

namespace OneTimeShare.Pages.Dashboard;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStorageService _storage;
    private readonly IAuditService _audit;

    public IndexModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        IStorageService storage, IAuditService audit)
    {
        _db = db;
        _userManager = userManager;
        _storage = storage;
        _audit = audit;
    }

    public IList<SharedAsset> Assets { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = _userManager.GetUserId(User)!;
        Assets = await _db.SharedAssets
            .Where(a => a.OwnerId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var userId = _userManager.GetUserId(User)!;
        var asset = await _db.SharedAssets.FirstOrDefaultAsync(a => a.Id == id && a.OwnerId == userId);
        if (asset is null) return NotFound();

        if (asset.BlobName is not null)
            await _storage.DeleteAsync(asset.BlobName);

        var token = asset.PublicToken;
        var name = asset.DisplayName;
        _db.SharedAssets.Remove(asset);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(AuditEventType.ShareDeleted, true,
            userId: userId, username: User.Identity?.Name,
            assetToken: token, assetName: name,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Success"] = $"Share \"{name}\" deleted.";
        return RedirectToPage();
    }
}
