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
using TimeLedger.Data;
using TimeLedger.Models;
using TimeLedger.Services;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace TimeLedger.Controllers
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
                .Include(e => e.Category)
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
                .Include(e => e.Category)
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
                allDay = e.AllDay,
                // 拡張メタをFullCalendarのextendedPropsに渡してUIで利用する
                source = e.Source.ToString(),
                type = e.Category?.Name ?? string.Empty,
                categoryId = e.CategoryId,
                categoryIcon = e.Category?.Icon ?? string.Empty,
                categoryColor = e.Category?.Color ?? string.Empty,
                priority = e.Priority.ToString(),
                location = e.Location,
                attendees = e.AttendeesCsv,
                recurrence = e.Recurrence.ToString(),
                reminder = e.ReminderMinutesBefore,
                recurrenceExceptions = e.RecurrenceExceptions
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
        public async Task<IActionResult> Create(string? startDate = null, string? endDate = null)
        {
            var model = new Event { Id = Guid.NewGuid().ToString("N") };
            var currentUser = await _userManager.GetUserAsync(User);
            model.UserId = currentUser?.Id ?? string.Empty;

            if (!string.IsNullOrEmpty(startDate))
            {
                if (DateTimeOffset.TryParse(startDate, out var dtoStart))
                    model.StartDate = dtoStart.LocalDateTime;
                else if (DateTime.TryParse(startDate, out var parsedStart))
                    model.StartDate = parsedStart;
            }
            if (model.StartDate == null)
                model.StartDate = DateTime.Now;

            if (!string.IsNullOrEmpty(endDate))
            {
                if (DateTimeOffset.TryParse(endDate, out var dtoEnd))
                    model.EndDate = dtoEnd.LocalDateTime;
                else if (DateTime.TryParse(endDate, out var parsedEnd))
                    model.EndDate = parsedEnd;
            }
            if (model.EndDate == null && model.StartDate.HasValue)
                model.EndDate = model.StartDate.Value.AddHours(1);

            await PopulateCategoriesAsync();
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
                if (model.Source == EventSource.Local && string.IsNullOrWhiteSpace(model.UID))
                    model.UID = null;

                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) return Unauthorized();
                model.UserId = currentUser.Id;
                model.LastModified = DateTime.UtcNow;

                _context.Events.Add(model);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            // モデルエラー時も時間フィールドが空にならないよう初期値を補完
            if (!model.StartDate.HasValue) model.StartDate = DateTime.Now;
            if (!model.EndDate.HasValue && model.StartDate.HasValue) model.EndDate = model.StartDate.Value.AddHours(1);
            LogModelStateErrors();
            await PopulateCategoriesAsync(model.CategoryId);
            return View(model);
        }

        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound("IDが指定されていません。");

            var ev = await _context.Events
                .Include(e => e.Category)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (ev == null) return NotFound("指定されたイベントが見つかりません。");

            await PopulateCategoriesAsync(ev.CategoryId);
            return View(ev);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, Event model)
        {
            if (id != model.Id) return BadRequest("IDが一致しません。");

            if (ModelState.IsValid)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) return Unauthorized();
                var existing = await _context.Events.FirstOrDefaultAsync(e => e.Id == id);
                if (existing == null) return NotFound($"ID({id})のイベントは存在しません。");

                // フォームからの値を既存エンティティに適用（UIDは保持）
                existing.Title = model.Title;
                existing.Description = model.Description;
                existing.StartDate = model.StartDate;
                existing.EndDate = model.EndDate;
                existing.AllDay = model.AllDay;
                existing.CategoryId = model.CategoryId;
                existing.Priority = model.Priority;
                existing.Location = model.Location;
                existing.AttendeesCsv = model.AttendeesCsv;
                existing.Recurrence = model.Recurrence;
                existing.ReminderMinutesBefore = model.ReminderMinutesBefore;
                existing.Source = model.Source;
                if (existing.Source == EventSource.Local && string.IsNullOrWhiteSpace(model.UID))
                {
                    existing.UID = null;
                }
                existing.LastModified = DateTime.UtcNow;

                // UID を保持しつつ、UID があればソースを iCloud に寄せる
                if (!string.IsNullOrWhiteSpace(existing.UID))
                {
                    existing.Source = EventSource.ICloud;
                }
                existing.UserId = currentUser.Id;

                try
                {
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
            LogModelStateErrors();
            await PopulateCategoriesAsync(model.CategoryId);
            return View(model);
        }

        public IActionResult Details(string id, string? occurrence = null)
        {
            if (string.IsNullOrEmpty(id)) return NotFound("イベントIDが指定されていません。");

            var ev = _context.Events
                .Include(e => e.Category)
                .FirstOrDefault(e => e.Id == id);
            if (ev == null) return NotFound($"ID({id})のイベントは存在しません。");

            if (!string.IsNullOrWhiteSpace(occurrence))
            {
                ViewBag.Occurrence = occurrence;
            }

            return View(ev);
        }

        [HttpGet]
        public IActionResult Delete(string id, string? occurrence = null)
        {
            var ev = _context.Events
                .Include(e => e.Category)
                .FirstOrDefault(e => e.Id == id);
            if (ev == null) return NotFound("削除対象のイベントが見つかりません。");

            if (!string.IsNullOrWhiteSpace(occurrence))
            {
                ViewBag.Occurrence = occurrence;
            }
            return View(ev);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id, string? mode, string? occurrence)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var ev = await _context.Events.FirstOrDefaultAsync(e => e.Id == id && (currentUser == null || e.UserId == currentUser.Id));
            if (ev != null)
            {
                if (ev.Recurrence != EventRecurrence.None && !string.IsNullOrWhiteSpace(mode))
                {
                    if (mode.Equals("single", StringComparison.OrdinalIgnoreCase))
                    {
                        if (DateTime.TryParse(occurrence, out var occDate))
                        {
                            var exceptions = (ev.RecurrenceExceptions ?? string.Empty)
                                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(x => x.Trim())
                                .Where(x => !string.IsNullOrWhiteSpace(x))
                                .ToHashSet(StringComparer.OrdinalIgnoreCase);
                            var key = occDate.Date.ToString("yyyy-MM-dd");
                            exceptions.Add(key);
                            ev.RecurrenceExceptions = string.Join(",", exceptions);
                            ev.LastModified = DateTime.UtcNow;
                            await _context.SaveChangesAsync();
                        }
                    }
                    else if (mode.Equals("future", StringComparison.OrdinalIgnoreCase))
                    {
                        if (DateTime.TryParse(occurrence, out var occDate))
                        {
                            var exceptions = (ev.RecurrenceExceptions ?? string.Empty)
                                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(x => x.Trim())
                                .Where(x => !string.IsNullOrWhiteSpace(x))
                                .ToHashSet(StringComparer.OrdinalIgnoreCase);
                            var token = $">={occDate.Date:yyyy-MM-dd}";
                            exceptions.Add(token);
                            ev.RecurrenceExceptions = string.Join(",", exceptions);
                            ev.LastModified = DateTime.UtcNow;
                            await _context.SaveChangesAsync();
                        }
                    }
                    else
                    {
                        _context.Events.Remove(ev);
                        await _context.SaveChangesAsync();
                    }
                }
                else
                {
                    _context.Events.Remove(ev);
                    await _context.SaveChangesAsync();
                }
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOccurrence(string id, string occurrenceDate)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(occurrenceDate))
                return BadRequest("IDまたは日付が不正です。");

            if (!DateTime.TryParse(occurrenceDate, out var parsedDate))
                return BadRequest("日付の形式が不正です。");
            parsedDate = parsedDate.Date;
            var ev = await _context.Events.FirstOrDefaultAsync(e => e.Id == id && e.UserId == currentUser.Id);
            if (ev == null) return NotFound("対象イベントが存在しません。");
            if (ev.Recurrence == EventRecurrence.None) return BadRequest("繰り返しイベントではありません。");

            var exceptions = (ev.RecurrenceExceptions ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var key = parsedDate.ToString("yyyy-MM-dd");
            exceptions.Add(key);
            ev.RecurrenceExceptions = string.Join(",", exceptions);
            ev.LastModified = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { message = "指定日の発生を削除しました", date = key });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOccurrencesFromDate(string id, string occurrenceDate)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(occurrenceDate))
                return BadRequest("IDまたは日付が不正です。");

            if (!DateTime.TryParse(occurrenceDate, out var parsedDate))
                return BadRequest("日付の形式が不正です。");
            parsedDate = parsedDate.Date;

            var ev = await _context.Events.FirstOrDefaultAsync(e => e.Id == id && e.UserId == currentUser.Id);
            if (ev == null) return NotFound("対象イベントが存在しません。");
            if (ev.Recurrence == EventRecurrence.None) return BadRequest("繰り返しイベントではありません。");

            var exceptions = (ev.RecurrenceExceptions ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var token = $">={parsedDate:yyyy-MM-dd}";
            exceptions.Add(token);
            ev.RecurrenceExceptions = string.Join(",", exceptions);
            ev.LastModified = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { message = "指定日以降の発生を削除しました", from = token });
        }

        private async Task PopulateCategoriesAsync(string? selectedId = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                ViewBag.CategoryOptions = new List<SelectListItem>();
                return;
            }

            var categories = await _context.Categories
                .Where(c => c.UserId == user.Id)
                .OrderBy(c => c.Name)
                .ToListAsync();
            if (!categories.Any())
            {
                categories = new List<CalendarCategory>
                {
                    new CalendarCategory { Name = "仕事・業務", Icon = "fa-briefcase", Color = "#3b82f6", UserId = user.Id },
                    new CalendarCategory { Name = "会議・打ち合わせ", Icon = "fa-people-group", Color = "#0ea5e9", UserId = user.Id },
                    new CalendarCategory { Name = "プライベート", Icon = "fa-house", Color = "#f97316", UserId = user.Id },
                    new CalendarCategory { Name = "締切・期限", Icon = "fa-bell", Color = "#ef4444", UserId = user.Id },
                    new CalendarCategory { Name = "学習・勉強", Icon = "fa-book-open", Color = "#10b981", UserId = user.Id }
                };
                _context.Categories.AddRange(categories);
                await _context.SaveChangesAsync();
            }

            ViewBag.CategoryOptions = categories.Select(c => new SelectListItem
            {
                Text = c.Name,
                Value = c.Id,
                Selected = !string.IsNullOrEmpty(selectedId) && c.Id == selectedId
            }).ToList();
        }

        private void LogModelStateErrors()
        {
            if (ModelState.IsValid) return;
            var errors = ModelState
                .Where(kvp => kvp.Value?.Errors?.Count > 0)
                .Select(kvp => $"{kvp.Key}: {string.Join(", ", kvp.Value!.Errors.Select(e => e.ErrorMessage))}");
            _logger.LogWarning("ModelState invalid: {Errors}", string.Join(" | ", errors));
        }
    }
}
