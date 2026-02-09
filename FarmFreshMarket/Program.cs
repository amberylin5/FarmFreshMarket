using FarmFreshMarket.Models;
using FarmFreshMarket.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Add HttpContextAccessor for AuditLogService
builder.Services.AddHttpContextAccessor();

// Add logging
builder.Services.AddLogging();

// Register Services
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<ISessionManagerService, SessionManagerService>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ITwoFactorService, TwoFactorService>();
builder.Services.AddScoped<ISmsService, SmsService>();

// Register EmailSettings
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<SmsSettings>(builder.Configuration.GetSection("SmsSettings"));

// Database
builder.Services.AddDbContext<AuthDbContext>();

// Identity Configuration with 1-minute lockout
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 12;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;

    // Lockout settings - 1 minute lockout after 3 failed attempts
    options.Lockout.MaxFailedAccessAttempts = 3;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(1); // 1 MINUTE LOCKOUT
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<AuthDbContext>()
.AddDefaultTokenProviders();

// Configure Application Cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Login";
    options.LogoutPath = "/Logout";
    options.AccessDeniedPath = "/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
    options.SlidingExpiration = true;
});

// Add session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(5);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Error Handling
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// ? FIXED: Add status code pages BEFORE other middleware
app.UseStatusCodePagesWithReExecute("/Error", "?statusCode={0}");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<FarmFreshMarket.Middleware.PasswordExpiryMiddleware>();

// Create admin user for testing
using (var scope = app.Services.CreateScope())
{
    try
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // Create Admin role
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
            Console.WriteLine("? Admin role created.");
        }

        // Create test admin user
        var adminEmail = "admin@test.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
            adminUser = new IdentityUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(adminUser, "AdminPass123!");

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                Console.WriteLine($"? Admin user created: {adminEmail} / AdminPass123!");
            }
            else
            {
                Console.WriteLine($"? Failed to create admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }
        else
        {
            var isAdmin = await userManager.IsInRoleAsync(adminUser, "Admin");
            if (!isAdmin)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                Console.WriteLine($"? Added existing user {adminEmail} to Admin role");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"? Error creating admin user: {ex.Message}");
    }
}

// Test endpoints
app.MapGet("/debug-db-2fa/{email}", async (string email, AuthDbContext context, UserManager<IdentityUser> userManager) =>
{
    var user = await userManager.FindByEmailAsync(email);
    if (user == null) return "User not found";

    // Check database directly
    var settings = await context.User2FASettings
        .AsNoTracking()
        .FirstOrDefaultAsync(s => s.UserId == user.Id);

    if (settings == null)
    {
        return $"? No User2FASettings found for user {email}\n" +
               $"User ID: {user.Id}";
    }

    return $"?? Database direct check for {email}:\n" +
           $"ID: {settings.Id}\n" +
           $"IsEnabled: {settings.IsEnabled}\n" +
           $"PreferredMethod: {settings.PreferredMethod}\n" +
           $"EnabledAt: {settings.EnabledAt}\n" +
           $"Email: {settings.Email}\n" +
           $"PhoneNumber: {settings.PhoneNumber}\n" +
           $"IsPhoneVerified: {settings.IsPhoneVerified}";
});

app.MapGet("/check-table-exists", async (AuthDbContext context) =>
{
    try
    {
        // Try to query the table
        var count = await context.User2FASettings.CountAsync();
        return $"? User2FASettings table exists with {count} records";
    }
    catch (Exception ex)
    {
        return $"? Error accessing User2FASettings table: {ex.Message}";
    }
});

app.MapGet("/test-2fa-status/{email}", async (string email, ITwoFactorService twoFactorService, UserManager<IdentityUser> userManager) =>
{
    var user = await userManager.FindByEmailAsync(email);
    if (user == null) return "User not found";

    var settings = await twoFactorService.Get2FASettingsAsync(user.Id);
    var isEnabled = await twoFactorService.Is2FAEnabledAsync(user.Id);

    return $"User: {email}\n" +
           $"2FA Enabled: {isEnabled}\n" +
           $"Settings.IsEnabled: {settings.IsEnabled}\n" +
           $"Preferred Method: {settings.PreferredMethod}\n" +
           $"Phone: {settings.PhoneNumber}\n" +
           $"Phone Verified: {settings.IsPhoneVerified}";
});

// Test email service
app.MapGet("/test-email-service", async (IEmailService emailService) =>
{
    try
    {
        await emailService.SendPasswordResetEmailAsync("test@test.com", "https://localhost:7046/ResetPassword?test=1");
        return "? EmailService test completed - check console";
    }
    catch (Exception ex)
    {
        return $"? EmailService ERROR: {ex.Message}";
    }
});

// Test XSS encoding
app.MapGet("/test-xss-encoding", () =>
{
    var testInput = "<script>alert('XSS')</script><img src='x' onerror='alert(1)'>";
    var encoded = System.Net.WebUtility.HtmlEncode(testInput);
    var decoded = System.Net.WebUtility.HtmlDecode(encoded);

    return $"Original: {testInput}\n" +
           $"Encoded: {encoded}\n" +
           $"Decoded: {decoded}\n" +
           $"Safe to display: {encoded}";
});

// Error test endpoints
app.MapGet("/test500", () => Results.StatusCode(500));
app.MapGet("/test404", () => Results.NotFound());
app.MapGet("/test403", (HttpContext context) =>
{
    context.Response.StatusCode = 403;
    return "Testing 403";
});

app.MapGet("/error-500", () => Results.Redirect("/Error?statusCode=500"));
app.MapGet("/error-404", () => Results.Redirect("/Error?statusCode=404"));
app.MapGet("/error-403", () => Results.Redirect("/Error?statusCode=403"));

app.MapRazorPages();
app.MapFallbackToPage("/Error");
app.MapGet("/health", () => "Application is running");

try
{
    Console.WriteLine("?? Starting application...");
    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"?? Application crashed: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    throw;
}