using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OneTimeShare.Data;
using OneTimeShare.Models;
using OneTimeShare.Services;

namespace OneTimeShare.Pages.Dashboard;

public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStorageService _storage;
    private readonly IEncryptionService _encryption;
    private readonly IAuditService _audit;

    private const long MaxFileBytes = 5 * 1024 * 1024;

    public CreateModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        IStorageService storage, IEncryptionService encryption, IAuditService audit)
    {
        _db = db;
        _userManager = userManager;
        _storage = storage;
        _encryption = encryption;
        _audit = audit;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required, MaxLength(200)]
        [Display(Name = "Name / Title")]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Type")]
        public string AssetType { get; set; } = "Secret";

        [Display(Name = "Secret text")]
        public string? SecretText { get; set; }

        [Display(Name = "File")]
        public IFormFile? File { get; set; }

        [Display(Name = "Share password")]
        [DataType(DataType.Password)]
        public string? SharePassword { get; set; }

        [Display(Name = "Max downloads / views")]
        [Range(1, int.MaxValue, ErrorMessage = "Must be at least 1.")]
        public int? MaxAccessCount { get; set; }

        [Display(Name = "Expires at")]
        public DateTime? ExpiresAt { get; set; }
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        var isFile = Input.AssetType == "File";

        if (isFile)
        {
            if (Input.File is null)
                ModelState.AddModelError("Input.File", "Please select a file.");
            else if (Input.File.Length > MaxFileBytes)
                ModelState.AddModelError("Input.File", "File exceeds 5 MB limit.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(Input.SecretText))
                ModelState.AddModelError("Input.SecretText", "Secret text is required.");
        }

        if (!ModelState.IsValid) return Page();

        var userId = _userManager.GetUserId(User)!;
        var assetKey = _encryption.GenerateAssetKey();
        var wrappedKey = _encryption.WrapKey(assetKey);

        byte[] plainContent;
        string? contentType = null;
        string? originalFileName = null;

        if (isFile)
        {
            using var ms = new MemoryStream();
            await Input.File!.CopyToAsync(ms);
            plainContent = ms.ToArray();
            contentType = Input.File.ContentType;
            originalFileName = Input.File.FileName;
        }
        else
        {
            plainContent = System.Text.Encoding.UTF8.GetBytes(Input.SecretText!);
            contentType = "text/plain";
        }

        var encryptedContent = _encryption.Encrypt(assetKey, plainContent);
        var token = TokenGenerator.Generate();
        var blobName = $"{userId}/{token}";

        await _storage.UploadAsync(blobName, encryptedContent);

        var asset = new SharedAsset
        {
            OwnerId = userId,
            PublicToken = token,
            AssetType = isFile ? Models.AssetType.File : Models.AssetType.Secret,
            DisplayName = Input.DisplayName,
            BlobName = blobName,
            ContentType = contentType,
            EncryptedKeyData = wrappedKey,
            PasswordHash = string.IsNullOrEmpty(Input.SharePassword)
                ? null
                : SharePasswordHelper.Hash(Input.SharePassword),
            MaxAccessCount = Input.MaxAccessCount,
            ExpiresAt = Input.ExpiresAt.HasValue
                ? DateTime.SpecifyKind(Input.ExpiresAt.Value, DateTimeKind.Local).ToUniversalTime()
                : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.SharedAssets.Add(asset);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(AuditEventType.Upload, true,
            userId: userId, username: User.Identity?.Name,
            assetToken: token, assetName: Input.DisplayName,
            details: isFile ? $"File: {originalFileName}, {plainContent.Length} bytes" : "Secret text",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Success"] = $"Share \"{Input.DisplayName}\" created.";
        return RedirectToPage("/Dashboard/Index");
    }
}
