using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pit2Hi022052.Data;
using Pit2Hi022052.Models;
using System.Threading.Tasks;

namespace Pit2Hi022052.Controllers
{
   // [Authorize(Roles = "Admin,user")]
    public class ICloudSettingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ICloudSettingController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // iCloud設定画面
        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            var setting = await _context.ICloudSettings
                .FirstOrDefaultAsync(x => x.UserId == currentUser.Id);

            return View(setting ?? new ICloudSetting());
        }

        // iCloud設定保存
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(ICloudSetting model)
        {
            var currentUser = await _userManager.GetUserAsync(User);

            if (!ModelState.IsValid)
            {
                return View("Index", model);
            }

            var existing = await _context.ICloudSettings
                .FirstOrDefaultAsync(x => x.UserId == currentUser.Id);

            if (existing != null)
            {
                // 更新
                existing.Username = model.Username;
                existing.Password = model.Password;
            }
            else
            {
                // 新規
                model.Id = Guid.NewGuid().ToString("N");
                model.UserId = currentUser.Id;
                _context.ICloudSettings.Add(model);
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "iCloud設定を保存しました。";

            return RedirectToAction(nameof(Index));
        }
    }
}
