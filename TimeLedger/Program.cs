using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TimeLedger.Data;
using TimeLedger.Models;
using TimeLedger.Extensions;
using TimeLedger.Services;
using TimeLedger.Middleware;
using System.Linq;
using Microsoft.AspNetCore.StaticFiles;


var builder = WebApplication.CreateBuilder(args);
// appsettings.{Environment}.json で接続先を環境ごとに切り替える。
// 本番のパスワードは環境変数や Secret Manager で上書きすること。

//================ DB接続 ==================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

//================ Identity登録 ===============
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

var authentication = builder.Services.AddAuthentication();

var outlookClientId = builder.Configuration["Authentication:Outlook:ClientId"];
var outlookClientSecret = builder.Configuration["Authentication:Outlook:ClientSecret"];
if (!string.IsNullOrWhiteSpace(outlookClientId) && !string.IsNullOrWhiteSpace(outlookClientSecret))
{
    authentication.AddMicrosoftAccount(CalendarAuthDefaults.OutlookScheme, options =>
    {
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.SaveTokens = true;
        options.ClientId = outlookClientId;
        options.ClientSecret = outlookClientSecret;
        options.AuthorizationEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize";
        options.TokenEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/token";
        options.CallbackPath = "/signin-outlook-calendar";
        options.Scope.Clear();
        foreach (var scope in CalendarAuthDefaults.OutlookScopes)
        {
            options.Scope.Add(scope);
        }
    });
}

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authentication.AddGoogle(CalendarAuthDefaults.GoogleScheme, options =>
    {
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.SaveTokens = true;
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.CallbackPath = "/signin-google-calendar";
        options.Scope.Clear();
        foreach (var scope in CalendarAuthDefaults.GoogleScopes)
        {
            options.Scope.Add(scope);
        }
        options.AccessType = "offline";
    });
}

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

//================ IHttpContextAccessor登録 ===============
builder.Services.AddHttpContextAccessor();

//================ iCloudCalDAVサービス登録 ===============
builder.Services.AddScoped<ICloudCalDavService, CloudCalDavService>();

//================ ICSパーサー登録 ===============
builder.Services.AddScoped<IcalParserService>();

//================ 外部カレンダー連携 ===============
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<OutlookCalendarClient>();
builder.Services.AddHttpClient<GoogleCalendarClient>();
builder.Services.AddScoped<IExternalCalendarClient, OutlookCalendarClient>();
builder.Services.AddScoped<IExternalCalendarClient, GoogleCalendarClient>();
builder.Services.AddScoped<ExternalCalendarSyncService>();
builder.Services.AddScoped<IOutlookCalendarService, OutlookCalendarService>();
builder.Services.AddScoped<IGoogleCalendarService, GoogleCalendarService>();

//================XXXXXX ===============
builder.Services.AddMemoryCache();
builder.Services.AddAntiforgery(o => o.HeaderName = "RequestVerificationToken");


//================ アプリ構築 ===============
var app = builder.Build();

//================ 起動時シード ===============
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        await SeedAdminUserAsync(userManager, roleManager);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Adminユーザーのシード中にエラーが発生しました。");
    }
}

var contentTypeProvider = new FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".webmanifest"] = "application/manifest+json";

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypeProvider
});
app.UseStatusCodePages();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseUserAccessLogging();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}/{id2?}");

app.MapRazorPages();
app.Run();

// Adminユーザーを起動時に冪等作成する
static async Task SeedAdminUserAsync(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
{
    const string adminEmail = "admin@admin.admin";
    const string adminPassword = "i2JvwXGn<>"; // 開発用の初期パスワード。本番では環境変数等に置き換える。
    const string adminRoleName = "Admin";

    var existing = await userManager.FindByEmailAsync(adminEmail);
    if (existing == null)
    {
        var adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };

        var createUserResult = await userManager.CreateAsync(adminUser, adminPassword);
        if (!createUserResult.Succeeded)
        {
            var errors = string.Join(", ", createUserResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Adminユーザー作成に失敗しました: {errors}");
        }

        existing = adminUser;
    }

    if (!await roleManager.RoleExistsAsync(adminRoleName))
    {
        var roleResult = await roleManager.CreateAsync(new IdentityRole(adminRoleName));
        if (!roleResult.Succeeded)
        {
            var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Adminロール作成に失敗しました: {errors}");
        }
    }

    if (!await userManager.IsInRoleAsync(existing, adminRoleName))
    {
        var addRoleResult = await userManager.AddToRoleAsync(existing, adminRoleName);
        if (!addRoleResult.Succeeded)
        {
            var errors = string.Join(", ", addRoleResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Adminロール付与に失敗しました: {errors}");
        }
    }
}
