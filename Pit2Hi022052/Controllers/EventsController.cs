using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Pit2Hi022052.Data;
using Pit2Hi022052.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Threading.Tasks;
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

        public EventsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ICloudCalDavService iCloudCalDavService,
            IcalParserService icalParserService,
            ILogger<EventsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _iCloudCalDavService = iCloudCalDavService;
            _icalParserService = icalParserService;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
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

            var dbEvents = _context.Events
                .Where(e => e.UserId == currentUser.Id)
                .ToList();

            _logger.LogInformation("DBイベント件数: {Count}", dbEvents.Count);

            List<Event> iCloudEvents = new List<Event>();

            try
            {
                _logger.LogInformation("iCloud CalDAVからイベントを取得中...");
                iCloudEvents = await _iCloudCalDavService.GetAllEventsAsync(); // ※UserId渡していない版
                _logger.LogInformation("iCloudイベント件数: {Count}", iCloudEvents.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "iCloudイベントの取得に失敗しました。");
            }

            var allEvents = dbEvents.Concat(iCloudEvents).ToList();
            _logger.LogInformation("結合後の全イベント件数: {Count}", allEvents.Count);

            var json = allEvents.Select(e => new
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
            model.UserId = currentUser?.Id;

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