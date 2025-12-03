using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeLedger.Data;
using TimeLedger.Models;
using TimeLedger.Services;

namespace TimeLedger.Controllers
{
    [Authorize]
    public class ExternalCalendarsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ExternalCalendarSyncService _syncService;
        private readonly IOutlookCalendarService _outlookService;
        private readonly IGoogleCalendarService _googleService;

        public ExternalCalendarsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ExternalCalendarSyncService syncService,
            IOutlookCalendarService outlookService,
            IGoogleCalendarService googleService)
        {
            _context = context;
            _userManager = userManager;
            _syncService = syncService;
            _outlookService = outlookService;
            _googleService = googleService;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sync(string provider, DateTime? from = null, DateTime? to = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            if (!Enum.TryParse<ExternalCalendarProvider>(provider, true, out var p))
                return BadRequest("provider が不正です");

            var isConnected = await HasConnectionAsync(user.Id, p, HttpContext.RequestAborted);
            if (!isConnected)
            {
                Response.StatusCode = 400;
                return View("LinkRequired", p);
            }

            var start = from ?? DateTime.UtcNow.AddDays(-30);
            var end = to ?? DateTime.UtcNow.AddDays(90);

            var saved = await _syncService.SyncAsync(user.Id, p, start, end);
            TempData["SyncResult"] = $"{provider} 同期: {saved} 件を保存/更新しました。";
            return RedirectToAction("Index", "Events");
        }

        private async Task<bool> HasConnectionAsync(string userId, ExternalCalendarProvider provider, CancellationToken ct)
        {
            return provider switch
            {
                ExternalCalendarProvider.Outlook => await _outlookService.GetConnectionAsync(userId, ct) != null,
                ExternalCalendarProvider.Google => await _googleService.GetConnectionAsync(userId, ct) != null,
                _ => false
            };
        }
    }
}
