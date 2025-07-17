using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pit2Hi022052.Data;
using Pit2Hi022052.Models;
using System.Threading.Tasks;

namespace Pit2Hi022052.Controllers
{
    //[Authorize(Roles = "Admin,user")]
    public class ICloudSettingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ICloudSettingController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // 一覧表示
        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var setting = await _context.ICloudSettings
                .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

            return View(setting);
        }

        // 登録画面
        [HttpGet]
        public IActionResult Create()
        {
            return View(new ICloudSetting());
        }

        // 登録処理
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ICloudSetting model)
        {
            var currentUser = await _userManager.GetUserAsync(User);

            if (await _context.ICloudSettings.AnyAsync(c => c.UserId == currentUser.Id))
            {
                ModelState.AddModelError("", "すでにiCloud設定が登録されています。");
                return RedirectToAction(nameof(Index));
            }

            model.UserId = currentUser.Id;
            _context.ICloudSettings.Add(model);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // 編集画面
        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var setting = await _context.ICloudSettings
                .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

            if (setting == null)
            {
                return RedirectToAction(nameof(Index));
            }

            return View(setting);
        }

        // 編集処理
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ICloudSetting updated)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var setting = await _context.ICloudSettings
                .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

            if (setting == null)
            {
                ModelState.AddModelError("", "iCloud設定が見つかりません。");
                return RedirectToAction(nameof(Index));
            }

            setting.Username = updated.Username;
            setting.Password = updated.Password;

            _context.ICloudSettings.Update(setting);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // 削除確認画面（オプション：不要なら省略可）
        [HttpGet]
        public async Task<IActionResult> Delete()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var setting = await _context.ICloudSettings
                .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

            if (setting == null)
            {
                return RedirectToAction(nameof(Index));
            }

            return View(setting);
        }

        // 削除処理
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var setting = await _context.ICloudSettings
                .FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

            if (setting != null)
            {
                _context.ICloudSettings.Remove(setting);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
