using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeLedger.Data;
using TimeLedger.Models;

namespace TimeLedger.Controllers
{
    [Authorize(Roles = RoleNames.Admin)]
    public class AppNoticesController : Controller
    {
        private readonly ApplicationDbContext _db;

        public AppNoticesController(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var notices = await _db.AppNotices
                .OrderByDescending(n => n.OccurredAt)
                .ThenByDescending(n => n.CreatedAtUtc)
                .ToListAsync();
            return View(notices);
        }

        public IActionResult Create()
        {
            return View(new AppNotice { OccurredAt = DateTime.UtcNow });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AppNotice input)
        {
            if (!ModelState.IsValid)
            {
                return View(input);
            }

            input.Id = string.IsNullOrWhiteSpace(input.Id) ? Guid.NewGuid().ToString("N") : input.Id;
            input.CreatedAtUtc = DateTime.UtcNow;
            _db.AppNotices.Add(input);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();
            var notice = await _db.AppNotices.FindAsync(id);
            if (notice == null) return NotFound();
            return View(notice);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, AppNotice input)
        {
            if (id != input.Id) return BadRequest();
            if (!ModelState.IsValid) return View(input);

            var notice = await _db.AppNotices.FindAsync(id);
            if (notice == null) return NotFound();

            notice.Kind = input.Kind;
            notice.Version = input.Version;
            notice.Title = input.Title;
            notice.Description = input.Description;
            notice.Highlights = input.Highlights;
            notice.OccurredAt = input.OccurredAt;
            notice.ResolvedAt = input.ResolvedAt;
            notice.Status = input.Status;

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();
            var notice = await _db.AppNotices.FindAsync(id);
            if (notice == null) return NotFound();
            return View(notice);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var notice = await _db.AppNotices.FindAsync(id);
            if (notice != null)
            {
                _db.AppNotices.Remove(notice);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
