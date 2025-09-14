using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Pit2Hi022052.Data;
using Pit2Hi022052.Models;
using Pit2Hi022052.Services;

namespace Pit2Hi022052.Controllers
{
    public class EventsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICloudCalDavService _iCloudCalDavService;
        private readonly IcalParserService _icalParserService;
        private readonly ILogger<EventsController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMemoryCache _cache;

        public EventsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ICloudCalDavService iCloudCalDavService,
            IcalParserService icalParserService,
            ILogger<EventsController> logger,
            IHttpContextAccessor httpContextAccessor,
            IMemoryCache cache)
        {
            _context = context;
            _userManager = userManager;
            _iCloudCalDavService = iCloudCalDavService;
            _icalParserService = icalParserService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _cache = cache;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var events = await _context.Events
                .Where(e => e.UserId == currentUser.Id)
                .OrderBy(e => e.StartDate ?? DateTime.MinValue)
                .ToListAsync();

            return View(events);
        }

        // ======= DBからの表示専用（同期はしない）=======
        [HttpGet]
        public async Task<JsonResult> GetEvents()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                _logger.LogWarning("未認証ユーザーからの GetEvents。");
                return new JsonResult(new { error = "ユーザーが未認証です。" });
            }

            var dbEvents = await _context.Events
                .Where(e => e.UserId == currentUser.Id)
                .OrderBy(e => e.StartDate ?? DateTime.MinValue)
                .ToListAsync();

            var json = dbEvents.Select(e => new
            {
                id = e.Id,
                title = e.Title,
                start = e.StartDate?.ToString("o", CultureInfo.InvariantCulture),
                end = e.EndDate?.ToString("o", CultureInfo.InvariantCulture),
                description = e.Description,
                allDay = e.AllDay
            });

            return new JsonResult(json);
        }

        // ======= 手動同期（60秒レート制御）=======
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sync()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var cacheKey = $"events:sync:{currentUser.Id}";
            if (_cache.TryGetValue(cacheKey, out _))
            {
                return StatusCode(429, new { message = "同期は60秒に1回までです。" });
            }

            _cache.Set(cacheKey, true, TimeSpan.FromSeconds(60));

            var sw = Stopwatch.StartNew();

            // 同期前のUID集合（保存件数の推定用）
            var beforeUids = await _context.Events
                .Where(e => e.UserId == currentUser.Id && e.UID != null && e.UID != "")
                .Select(e => e.UID!)
                .ToHashSetAsync();

            List<Event> pulled;
            try
            {
                pulled = await _iCloudCalDavService.GetAllEventsAsync(currentUser.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "手動同期に失敗");
                return StatusCode(500, new { message = "同期に失敗しました。" });
            }

            // 新規保存されたであろう件数（サービス内のロジックと対応）
            var saved = pulled
                .Where(ev => !string.IsNullOrWhiteSpace(ev.UID))
                .Select(ev => ev.UID!)
                .Distinct(StringComparer.Ordinal)
                .Count(uid => !beforeUids.Contains(uid));

            sw.Stop();
            return Json(new { saved, scanned = pulled.Count, durationMs = sw.ElapsedMilliseconds });
        }

        [HttpGet]
        public async Task<IActionResult> Create(string startDate = null, string endDate = null)
        {
            var model = new Event { Id = Guid.NewGuid().ToString("N") };
            var currentUser = await _userManager.GetUserAsync(User);
            model.UserId = currentUser?.Id ?? string.Empty;

            if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var parsedStart))
                model.StartDate = parsedStart;

            if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var parsedEnd))
                model.EndDate = parsedEnd;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Event model)
        {
            if (ModelState.IsValid)
            {
                if (string.IsNullOrEmpty(model.Id))
                    model.Id = Guid.NewGuid().ToString("N");

                var currentUser = await _userManager.GetUserAsync(User);
                model.UserId = currentUser?.Id ?? string.Empty;

                _context.Events.Add(model);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound("IDが指定されていません。");

            var ev = await _context.Events.FindAsync(id);
            if (ev == null) return NotFound("指定されたイベントが見つかりません。");

            return View(ev);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, Event model)
        {
            if (id != model.Id) return BadRequest("IDが一致しません。");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(model);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Events.Any(e => e.Id == id))
                        return NotFound($"ID({id})のイベントは存在しません。");
                    else throw;
                }
            }
            return View(model);
        }

        public IActionResult Details(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound("イベントIDが指定されていません。");

            var ev = _context.Events.FirstOrDefault(e => e.Id == id);
            if (ev == null) return NotFound($"ID({id})のイベントは存在しません。");

            return View(ev);
        }

        [HttpGet]
        public IActionResult Delete(string id)
        {
            var ev = _context.Events.FirstOrDefault(e => e.Id == id);
            if (ev == null) return NotFound("削除対象のイベントが見つかりません。");
            return View(ev);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(string id, bool confirm)
        {
            var ev = _context.Events.FirstOrDefault(e => e.Id == id);
            if (ev != null)
            {
                _context.Events.Remove(ev);
                _context.SaveChanges();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
