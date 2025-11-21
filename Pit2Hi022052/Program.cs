using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pit2Hi022052.Data;
using Pit2Hi022052.Models;
using Pit2Hi022052.Extensions;
using Pit2Hi022052.Services;


var builder = WebApplication.CreateBuilder(args);

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
app.UseStaticFiles();
app.UseStatusCodePages();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}/{id2?}");

app.MapRazorPages();
app.Run();
