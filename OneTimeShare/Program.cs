using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OneTimeShare.Data;
using OneTimeShare.Models;
using OneTimeShare.Services;

var builder = WebApplication.CreateBuilder(args);

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Identity – no email confirmation required
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequiredLength = 8;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});

// Application services
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IEncryptionService, EncryptionService>();
builder.Services.AddScoped<AdminBootstrapService>();

// Storage: Azure in production, local filesystem in development
var storageProvider = builder.Configuration["Storage:Provider"] ?? "Azure";
if (storageProvider.Equals("Local", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddScoped<IStorageService, LocalStorageService>();
else
    builder.Services.AddScoped<IStorageService, AzureBlobStorageService>();

// File upload limit: 5 MB
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 5 * 1024 * 1024;
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 5 * 1024 * 1024 + 4096; // slight headroom for form fields
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Dashboard");
    options.Conventions.AuthorizeFolder("/Profile");
    options.Conventions.AuthorizeFolder("/Admin", "AdminOnly");
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(AdminBootstrapService.AdminRole));
});

var app = builder.Build();

// Run DB migration and seed on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();

    var bootstrap = scope.ServiceProvider.GetRequiredService<AdminBootstrapService>();
    await bootstrap.EnsureRolesAsync();
    await bootstrap.SeedFirstAdminAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.Run();
