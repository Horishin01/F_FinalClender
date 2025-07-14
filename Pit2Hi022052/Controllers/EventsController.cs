using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Pit2Hi022052.Data;
using Pit2Hi022052.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

// 追加
using System.Threading.Tasks;

namespace Pit2Hi022052.Controllers
{
    public class EventsController : Controller
    {
        private readonly ApplicationDbContext _context;
        protected virtual UserManager<ApplicationUser> UserManager { get; }

        // iCloud連携用サービス
        private readonly ICloudCalDavService _iCloudCalDavService;
        private readonly IcalParserService _icalParserService;
        private readonly ILogger<EventsController> _logger;

        public EventsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ICloudCalDavService iCloudCalDavService,
            IcalParserService icalParserService,
             ILogger<EventsController> logger 
        )
        {
            _context = context;
            UserManager = userManager;
            _iCloudCalDavService = iCloudCalDavService;
            _icalParserService = icalParserService;
            _logger = logger;
        }

    //    [Authorize(Roles = "Admin,user")]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<JsonResult> GetEvents()
        {
            var currentUser = await UserManager.GetUserAsync(User);

            _logger.LogInformation("🌿 [GetEvents] ユーザー {User} のイベントを取得します", currentUser?.UserName);

            // 1. DBイベント取得
            var dbEvents = _context.Events
                .Where(e => e.UserId == currentUser.Id)
                .ToList();

            _logger.LogInformation("✅ DBイベント件数: {Count}", dbEvents.Count);

            // 2. iCloudイベント取得
            List<Event> iCloudEvents = new List<Event>();
            try
            {
                iCloudEvents = await _iCloudCalDavService.GetAllEventsAsync();

                if (iCloudEvents.Count == 0)
                {
                    _logger.LogWarning("⚠ iCloudから取得したイベントは0件です。");
                }
                else
                {
                    _logger.LogInformation("✅ iCloudイベント件数: {Count}", iCloudEvents.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ iCloudイベントの取得に失敗しました。");
            }

            // 3. 結合
            var allEvents = dbEvents.Concat(iCloudEvents).ToList();
            _logger.LogInformation("🌱 結合後の全イベント件数: {Count}", allEvents.Count);

            // 4. JSONに変換
            var json = allEvents.Select(e => new
            {
                id = e.Id,
                title = e.Title,
                start = e.StartDate.HasValue
                    ? e.StartDate.Value.ToString("o", CultureInfo.InvariantCulture)
                    : null,
                end = e.EndDate.HasValue
                    ? e.EndDate.Value.ToString("o", CultureInfo.InvariantCulture)
                    : null,
                description = e.Description
            });

            return new JsonResult(json);
        }


     //   [Authorize(Roles = "Admin,user")]
        [HttpGet]
        public async Task<IActionResult> Create(string startDate = null, string endDate = null)
        {
            var model = new Event();
            model.Id = Guid.NewGuid().ToString("N");
            var currentUser = await UserManager.GetUserAsync(User);
            model.UserId = currentUser.Id;

            if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var parsedStartDate))
            {
                model.StartDate = parsedStartDate;
            }
            if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var parsedEndDate))
            {
                model.EndDate = parsedEndDate;
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Event model)
        {
            if (ModelState.IsValid)
            {
                _context.Events.Add(model);
                _context.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(model);
        }

    //    [Authorize(Roles = "Admin,user")]
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound("IDが指定されていません。");
            }

            var ev = await _context.Events.FindAsync(id);
            if (ev == null)
            {
                return NotFound("指定されたイベントが見つかりません。");
            }

            return View(ev);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, Event model)
        {
            if (id != model.Id)
            {
                return BadRequest("IDが一致しません。");
            }

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
                    {
                        return NotFound($"指定されたID({id})のイベントは存在しません。");
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return View(model);
        }

    //    [Authorize(Roles = "Admin,user")]
        public IActionResult Details(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound("イベントIDが指定されていません。");
            }

            var ev = _context.Events.FirstOrDefault(e => e.Id == id);
            if (ev == null)
            {
                return NotFound($"指定されたID({id})のイベントは存在しません。");
            }

            return View(ev);
        }

        [HttpGet]
        public IActionResult Delete(string id)
        {
            var ev = _context.Events.FirstOrDefault(e => e.Id == id);
            if (ev == null)
            {
                return NotFound("削除対象のイベントが見つかりません。");
            }
            return View(ev);
        }

        [Authorize(Roles = "Admin,user")]
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
            return RedirectToAction("Index");
        }
    }
}
