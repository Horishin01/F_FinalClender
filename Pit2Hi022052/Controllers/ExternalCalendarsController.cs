using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pit2Hi022052.Data;
using Pit2Hi022052.Models;
using Pit2Hi022052.Services;

namespace Pit2Hi022052.Controllers
{
    public class ExternalCalendarsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ExternalCalendarSyncService _syncService;

        public ExternalCalendarsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ExternalCalendarSyncService syncService)
        {
            _context = context;
            _userManager = userManager;
            _syncService = syncService;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sync(string provider, DateTime? from = null, DateTime? to = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            if (!Enum.TryParse<ExternalCalendarProvider>(provider, true, out var p))
                return BadRequest("provider が不正です");

            var start = from ?? DateTime.UtcNow.AddDays(-30);
            var end = to ?? DateTime.UtcNow.AddDays(90);

            var saved = await _syncService.SyncAsync(user.Id, p, start, end);
            TempData["SyncResult"] = $"{provider} 同期: {saved} 件を保存/更新しました。";
            return RedirectToAction("Index", "Events");
        }
    }
}
