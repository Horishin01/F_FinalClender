// Program.cs
// アプリのエントリーポイント。DI/認証プロバイダ/カレンダー連携クライアントの登録と、Admin ユーザーのシードを行う。
// 外部 OAuth のクライアントID/Secret は環境変数、Development Local設定、User Secretsのいずれかで設定し、起動時に存在する場合のみ追加する。

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TimeLedger.Data;
using TimeLedger.Models;
using TimeLedger.Extensions;
using TimeLedger.Services;
using TimeLedger.Middleware;
using TimeLedger.Security;
using System.Linq;
using Microsoft.AspNetCore.StaticFiles;


var builder = WebApplication.CreateBuilder(args);
// appsettings.{Environment}.json で接続先を環境ごとに切り替える。
// DevelopmentではGit管理外のappsettings.Development.Local.jsonも使用できる。
// ローカルJSONより環境変数とコマンドラインを優先し、ProductionではローカルJSONを読み込まない。
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile(
        "appsettings.Development.Local.json",
        optional: true,
        reloadOnChange: true);
    builder.Configuration.AddEnvironmentVariables();
    builder.Configuration.AddCommandLine(args);
}

var configuredPathBase = NormalizePathBase(
    builder.Configuration["PathBase"]
    ?? builder.Configuration["ASPNETCORE_PATHBASE"]);

WebTransportSecurity webTransportSecurity;
try
{
    webTransportSecurity = WebTransportSecurity.Load(builder.Configuration, builder.Environment);
}
catch (InvalidOperationException ex) when (ex.Message.StartsWith("TIMELEDGER-", StringComparison.Ordinal))
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 2;
    return;
}

webTransportSecurity.ConfigureServices(builder.Services);

if (args.Contains("--validate-web-security", StringComparer.Ordinal))
{
    Console.WriteLine($"Web通信構成: {webTransportSecurity.Description}");
    return;
}

//================ DB接続 ==================
// DevelopmentはDockerやDBサーバーを使わず、プロジェクト配下のSQLiteファイルだけで動作する。
// 以前のDevelopment用PostgreSQL接続文字列がLocal設定に残っていても参照しない。
var useDevelopmentSqlite = builder.Environment.IsDevelopment();
string connectionString;
if (useDevelopmentSqlite)
{
    var developmentDatabasePath = Path.Combine(builder.Environment.ContentRootPath, "timeledger.db");
    connectionString = $"Data Source={developmentDatabasePath}";
}
else
{
    try
    {
        connectionString = GetRequiredConnectionString(builder.Configuration, builder.Environment);
    }
    catch (InvalidOperationException ex) when (ex.Message.StartsWith("TIMELEDGER-", StringComparison.Ordinal))
    {
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 2;
        return;
    }
}

if (args.Contains("--validate-db-configuration", StringComparer.Ordinal))
{
    var configurationDescription = useDevelopmentSqlite
        ? "開発用SQLiteファイルを使用"
        : $"DefaultConnection設定済み（{builder.Environment.EnvironmentName}）";
    Console.WriteLine($"DB接続構成: {configurationDescription}。接続先には接続しません。");
    return;
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (useDevelopmentSqlite)
    {
        options.UseSqlite(connectionString);
    }
    else
    {
        options.UseNpgsql(connectionString);
    }
});

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

//================ カレンダータイムゾーン設定 ===============
builder.Services.Configure<CalendarSettings>(builder.Configuration.GetSection("Calendar"));
builder.Services.AddSingleton<ICalendarTimeZoneService, CalendarTimeZoneService>();
builder.Services.Configure<DiscordNotificationSettings>(builder.Configuration.GetSection(DiscordNotificationSettings.SectionName));

//================ IHttpContextAccessor登録 ===============
builder.Services.AddHttpContextAccessor();

//================ iCloudCalDAVサービス登録 ===============
builder.Services.AddScoped<ICloudCalDavService, CloudCalDavService>();

//================ ICSパーサー登録 ===============
builder.Services.AddScoped<IcalParserService>();

//================ 外部カレンダー連携 ===============
builder.Services.AddHttpClient();
builder.Services.AddHttpClient(nameof(DiscordReminderWorker));
builder.Services.AddHostedService<DiscordReminderWorker>();
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

if (useDevelopmentSqlite)
{
    using var databaseScope = app.Services.CreateScope();
    var database = databaseScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    database.Database.EnsureCreated();
}

if (webTransportSecurity.RequireHttps)
{
    app.UseForwardedHeaders();
}

//================ 起動時シード ===============
if (builder.Configuration.GetValue<bool>("BootstrapAdmin:Enabled"))
{
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("初期管理者の自動作成はDevelopment環境だけで使用できます。");
    }

    var bootstrapAdminEmail = builder.Configuration["BootstrapAdmin:Email"];
    var bootstrapAdminPassword = builder.Configuration["BootstrapAdmin:Password"];
    if (string.IsNullOrWhiteSpace(bootstrapAdminEmail) || string.IsNullOrWhiteSpace(bootstrapAdminPassword))
    {
        throw new InvalidOperationException(
            "BootstrapAdminを有効にする場合はEmailとPasswordをappsettings.Development.Local.json、環境変数、またはUser Secretsで設定してください。");
    }

    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        await SeedAdminUserAsync(userManager, roleManager, bootstrapAdminEmail, bootstrapAdminPassword);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "開発用Adminユーザーのシード中にエラーが発生しました。");
    }
}

var contentTypeProvider = new FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".webmanifest"] = "application/manifest+json";

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    if (webTransportSecurity.RequireHttps)
    {
        app.UseHsts();
    }
}

if (!string.IsNullOrEmpty(configuredPathBase))
{
    app.UsePathBase(configuredPathBase);
}

if (webTransportSecurity.RequireHttps)
{
    app.Use(async (context, next) =>
    {
        if (!context.Request.IsHttps)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync("HTTPS経由の要求だけを受け付けます。");
            return;
        }

        await next();
    });
}

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
static async Task SeedAdminUserAsync(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    string adminEmail,
    string adminPassword)
{
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

static string? NormalizePathBase(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return null;
    }

    var normalized = value.Trim();
    if (normalized == "/")
    {
        return null;
    }

    if (!normalized.StartsWith('/'))
    {
        normalized = "/" + normalized;
    }

    if (normalized.Length > 1 && normalized.EndsWith('/'))
    {
        normalized = normalized.TrimEnd('/');
    }

    return normalized;
}

static string GetRequiredConnectionString(IConfiguration configuration, IHostEnvironment environment)
{
    var connectionString = configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        return connectionString;
    }

    var environmentCode = environment.IsProduction()
        ? "PRODUCTION"
        : environment.IsDevelopment()
            ? "DEVELOPMENT"
            : "NONPRODUCTION";
    var configurationSource = environment.IsProduction()
        ? "環境変数または承認済み秘密情報ストア"
        : "appsettings.Development.Local.json、環境変数、またはUser Secrets";

    throw new InvalidOperationException(
        $"TIMELEDGER-{environmentCode}-DB-CONNECTION-MISSING: ConnectionStrings:DefaultConnectionが未設定です。{configurationSource}で設定してください。");
}
