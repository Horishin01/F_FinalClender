using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TimeLedger.Data;
using TimeLedger.Models;

namespace TimeLedger.Areas.Identity.Pages.Account.Manage
{
    [Authorize]
    public class ICloudSettingModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public ICloudSettingModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public ICloudSetting? Setting { get; private set; }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required, EmailAddress]
            public string Username { get; set; } = string.Empty;

            // 更新時は任意（空なら据え置き）
            public string? Password { get; set; }
        }

        [TempData]
        public string? StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!AlphaFeatureFlags.AccountAlphaFeatures) return NotFound();
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Challenge();
            if (!await IsAdminAsync(user)) return Forbid();

            Setting = await _db.ICloudSettings.AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == user.Id);

            // 既存があれば編集フォームに初期値を入れておく
            if (Setting is not null)
            {
                Input.Username = Setting.Username;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostSaveAsync()
        {
            if (!AlphaFeatureFlags.AccountAlphaFeatures) return NotFound();
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Challenge();
            if (!await IsAdminAsync(user)) return Forbid();

            // 既存取得
            var setting = await _db.ICloudSettings
                .FirstOrDefaultAsync(x => x.UserId == user.Id);

            if (setting is null)
            {
                // 新規作成：Password は必須
                if (!ModelState.IsValid || string.IsNullOrWhiteSpace(Input.Password))
                {
                    StatusMessage = "入力に不備があります。新規登録はパスワード必須です。";
                    await OnGetAsync();
                    return Page();
                }

                setting = new ICloudSetting
                {
                    Id = Guid.NewGuid().ToString("N"),
                    UserId = user.Id,
                    Username = Input.Username.Trim(),
                    Password = Input.Password.Trim()
                };
                _db.ICloudSettings.Add(setting);
                await _db.SaveChangesAsync();
                StatusMessage = "iCloud設定を登録しました。";
            }
            else
            {
                if (!ModelState.IsValid)
                {
                    StatusMessage = "入力に不備があります。";
                    await OnGetAsync();
                    return Page();
                }

                setting.Username = Input.Username.Trim();
                if (!string.IsNullOrWhiteSpace(Input.Password))
                {
                    setting.Password = Input.Password.Trim();
                }
                await _db.SaveChangesAsync();
                StatusMessage = "iCloud設定を更新しました。";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync()
        {
            if (!AlphaFeatureFlags.AccountAlphaFeatures) return NotFound();
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Challenge();
            if (!await IsAdminAsync(user)) return Forbid();

            var setting = await _db.ICloudSettings
                .FirstOrDefaultAsync(x => x.UserId == user.Id);

            if (setting is not null)
            {
                _db.ICloudSettings.Remove(setting);
                await _db.SaveChangesAsync();
                StatusMessage = "iCloud設定を削除しました。";
            }
            return RedirectToPage();
        }

        private Task<bool> IsAdminAsync(ApplicationUser user)
            => _userManager.IsInRoleAsync(user, RoleNames.Admin);
    }
}
