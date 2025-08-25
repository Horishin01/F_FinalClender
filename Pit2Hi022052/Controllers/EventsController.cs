using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pit2Hi022052.Data;
using Pit2Hi022052.Models;
using Pit2Hi022052.Services;

namespace Pit2Hi022052.Controllers
{
    public class EventsController : Controller
    {
        // ================================
        // フィールド定義
        // ================================
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICloudCalDavService _iCloudCalDavService;
        private readonly IcalParserService _icalParserService;
        private readonly ILogger<EventsController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        // ================================
        // コンストラクタ（依存性注入）
        // ================================
        public EventsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ICloudCalDavService iCloudCalDavService,
            IcalParserService icalParserService,
            ILogger<EventsController> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _userManager = userManager;
            _iCloudCalDavService = iCloudCalDavService;
            _icalParserService = icalParserService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        // ================================
        // Index アクション（イベント一覧表示）
        // ================================
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

        [HttpGet]
        public async Task<JsonResult> GetEvents()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                _logger.LogWarning("ユーザーが取得できませんでした。ログインが必要です。");
                return new JsonResult(new { error = "ユーザーが未認証です。" });
            }

            _logger.LogInformation("[GetEvents] ユーザー {User} のイベントを取得します", currentUser.UserName);

            // iCloud → DB 同期（保存のみ。表示には使わない）
            try
            {
                _logger.LogInformation("iCloud CalDAVからイベントを取得中...");
                var pulled = await _iCloudCalDavService.GetAllEventsAsync(currentUser.Id);
                _logger.LogInformation("iCloudイベント取得: {Count} 件（保存対象はサービス内で判定）", pulled.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "iCloudイベントの取得に失敗しました。");
            }

            // 表示はDBのみ
            var dbEvents = await _context.Events
                .Where(e => e.UserId == currentUser.Id)
                .OrderBy(e => e.StartDate ?? DateTime.MinValue)
                .ToListAsync();

            _logger.LogInformation("DBイベント件数: {Count}", dbEvents.Count);

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
