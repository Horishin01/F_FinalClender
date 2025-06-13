/*----------------------------------------------------------
 Program.cs
----------------------------------------------------------*/
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.UI;
using Pit2Hi022999.Data;
using Pit2Hi022999.Models;
//==========================================================
// トップレベルステートメント
// エントリーポイント

// -- アプリケーションビルダの生成 --
var builder = WebApplication.CreateBuilder(args);
// -- アプリケーションビルダへのサービスの追加 –
var connectionString = builder.Configuration.GetConnectionString
("DefaultConnection") ?? throw new InvalidOperationException
("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>
(options => options.UseNpgsql(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddDefaultIdentity<ApplicationUser>
(options => options.SignIn.RequireConfirmedAccount = true)
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();
// -- アプリケーションビルダによるアプリケーションの生成 --
var app = builder.Build();

// -- アプリケーションの設定 --
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days.
    // You may want to change this for production scenarios,
    // see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseStatusCodePages();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute
(
name: "default",
pattern: "{controller=Home}/{action=Index}/{id?}/{id2?}"
); 
app.MapRazorPages();
// -- アプリケーションの実行 --
app.Run();
// -- 終 了 --

return;
//==========================================================
// END
//==========================================================