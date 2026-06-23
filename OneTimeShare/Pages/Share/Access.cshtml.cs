using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OneTimeShare.Data;
using OneTimeShare.Models;
using OneTimeShare.Services;
using System.ComponentModel.DataAnnotations;

namespace OneTimeShare.Pages.Share;

public class AccessModel : PageModel
{
    public enum PageState { NotFound, Expired, LimitReached, NeedsPassword, ShowSecret, FileDownload }

    private readonly ApplicationDbContext _db;
    private readonly IStorageService _storage;
    private readonly IEncryptionService _encryption;
    private readonly IAuditService _audit;

    public AccessModel(ApplicationDbContext db, IStorageService storage,
        IEncryptionService encryption, IAuditService audit)
    {
        _db = db;
        _storage = storage;
        _encryption = encryption;
        _audit = audit;
    }

    [FromRoute]
    public string Token { get; set; } = string.Empty;

    [BindProperty]
    public string? SharePassword { get; set; }

    public PageState State { get; private set; }
    public string? SecretText { get; private set; }
    public string? EncodedPassword { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var asset = await _db.SharedAssets.FirstOrDefaultAsync(a => a.PublicToken == Token);
        return await ResolveStateAsync(asset, sharePassword: null);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var asset = await _db.SharedAssets.FirstOrDefaultAsync(a => a.PublicToken == Token);
        return await ResolveStateAsync(asset, SharePassword);
    }

    private async Task<IActionResult> ResolveStateAsync(SharedAsset? asset, string? sharePassword)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        if (asset is null)
        {
            await _audit.LogAsync(AuditEventType.ShareNotFound, false,
                assetToken: Token, ipAddress: ip);
            State = PageState.NotFound;
            return Page();
        }

        if (asset.IsExpired)
        {
            await _audit.LogAsync(AuditEventType.ShareExpired, false,
                assetToken: Token, assetName: asset.DisplayName, ipAddress: ip);
            State = PageState.Expired;
            return Page();
        }

        if (asset.IsLimitReached)
        {
            await _audit.LogAsync(AuditEventType.ShareLimitReached, false,
                assetToken: Token, assetName: asset.DisplayName, ipAddress: ip);
            State = PageState.LimitReached;
            return Page();
        }

        // Password check
        if (asset.PasswordHash is not null)
        {
            if (string.IsNullOrEmpty(sharePassword))
            {
                State = PageState.NeedsPassword;
                return Page();
            }

            if (!SharePasswordHelper.Verify(sharePassword, asset.PasswordHash))
            {
                await _audit.LogAsync(AuditEventType.ShareInvalidPassword, false,
                    assetToken: Token, assetName: asset.DisplayName, ipAddress: ip);
                ModelState.AddModelError(string.Empty, "Incorrect password.");
                State = PageState.NeedsPassword;
                return Page();
            }
        }

        // Increment counter
        asset.AccessCount++;
        asset.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(AuditEventType.Download, true,
            assetToken: Token, assetName: asset.DisplayName,
            details: asset.AssetType.ToString(), ipAddress: ip);

        if (asset.AssetType == AssetType.File)
        {
            State = PageState.FileDownload;
            EncodedPassword = sharePassword is null ? string.Empty
                : Uri.EscapeDataString(sharePassword);
            return Page();
        }

        // Decrypt secret inline
        var encryptedContent = await _storage.DownloadAsync(asset.BlobName!);
        var assetKey = _encryption.UnwrapKey(asset.EncryptedKeyData!);
        var plaintext = _encryption.Decrypt(assetKey, encryptedContent);
        SecretText = System.Text.Encoding.UTF8.GetString(plaintext);

        State = PageState.ShowSecret;
        return Page();
    }

    /// <summary>Direct file download handler at /s/{token}/download.</summary>
    public async Task<IActionResult> OnGetDownloadAsync(string? pwd)
    {
        var asset = await _db.SharedAssets.FirstOrDefaultAsync(a => a.PublicToken == Token);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        if (asset is null || asset.AssetType != AssetType.File)
            return NotFound();
        if (asset.IsExpired || asset.IsLimitReached)
            return BadRequest("Share no longer available.");

        if (asset.PasswordHash is not null)
        {
            if (string.IsNullOrEmpty(pwd) || !SharePasswordHelper.Verify(pwd, asset.PasswordHash))
            {
                await _audit.LogAsync(AuditEventType.ShareInvalidPassword, false,
                    assetToken: Token, assetName: asset.DisplayName, ipAddress: ip);
                return Unauthorized();
            }
        }

        var encryptedContent = await _storage.DownloadAsync(asset.BlobName!);
        var assetKey = _encryption.UnwrapKey(asset.EncryptedKeyData!);
        var plaintext = _encryption.Decrypt(assetKey, encryptedContent);

        var contentType = asset.ContentType ?? "application/octet-stream";
        var fileName = asset.DisplayName;

        return File(plaintext, contentType, fileName);
    }
}
