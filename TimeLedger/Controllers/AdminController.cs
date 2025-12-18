// AdminController
// 管理者ポータルのサマリー表示専用。ユーザー/ロール/イベント数や外部連携数などを集計してビューへ渡す。

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeLedger.Data;
using TimeLedger.Models;
using TimeLedger.ViewModels;

namespace TimeLedger.Controllers
{
    [Authorize(Roles = RoleNames.Admin)]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;

        public AdminController(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var nowUtc = DateTime.UtcNow;
            var last7Days = nowUtc.AddDays(-7);
            var last30Days = nowUtc.AddDays(-30);

            var model = new AdminPortalViewModel
            {
                TotalUsers = await _db.Users.CountAsync(cancellationToken),
                TotalRoles = await _db.Roles.CountAsync(cancellationToken),
                EventsLast30Days = await _db.Events!.CountAsync(e => e.StartDate >= last30Days, cancellationToken),
                Categories = await _db.Categories!.CountAsync(cancellationToken),
                AccessLogsLast7Days = await _db.UserAccessLogs.CountAsync(l => l.AccessedAtUtc >= last7Days, cancellationToken),
                OutlookConnections = await _db.OutlookCalendarConnections.CountAsync(cancellationToken),
                GoogleConnections = await _db.GoogleCalendarConnections.CountAsync(cancellationToken),
                ExternalLegacyAccounts = await _db.ExternalCalendarAccounts!.CountAsync(cancellationToken)
            };

            return View(model);
        }
    }
}
