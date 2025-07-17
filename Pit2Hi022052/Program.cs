using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pit2Hi022052.Data;
using Pit2Hi022052.Models;
using Pit2Hi022052.Services;

var builder = WebApplication.CreateBuilder(args);

//================ ログレベルの設定 ==================
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Debug); // ← 重要：CalDAVエラー等の追跡用

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

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

//================ IHttpContextAccessor登録 ===============
builder.Services.AddHttpContextAccessor();

//================ iCloudCalDAVサービス登録 ===============
builder.Services.AddScoped<ICloudCalDavService, CloudCalDavService>();

//================ ICSパーサー登録 ===============
builder.Services.AddScoped<IcalParserService>();

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

// HTTPSポートが未指定でも開発時は続行できるようにしておく
app.UseHttpsRedirection(); // ← 本番用だが、localhostなら無視してもOK
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
