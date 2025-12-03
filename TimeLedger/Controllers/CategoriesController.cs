using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeLedger.Data;
using TimeLedger.Models;

namespace TimeLedger.Controllers
{
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CategoriesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            var list = await _context.Categories
                .Where(c => c.UserId == user.Id)
                .OrderBy(c => c.Name)
                .ToListAsync();
            return View(list);
        }

        public IActionResult Create()
        {
            ViewBag.AvailableIcons = GetIcons();
            return View(new CalendarCategory());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CalendarCategory category)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // UserId はサーバーで付与するため検証対象から除外
            ModelState.Remove("UserId");
            category.UserId = user.Id;

            if (!ModelState.IsValid)
            {
                ViewBag.AvailableIcons = GetIcons();
                return View(category);
            }

            if (string.IsNullOrWhiteSpace(category.Id))
            {
                category.Id = Guid.NewGuid().ToString("N");
            }

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            var cat = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id && c.UserId == user.Id);
            if (cat == null) return NotFound();
            ViewBag.AvailableIcons = GetIcons();
            return View(cat);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, CalendarCategory category)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            ModelState.Remove("UserId");
            category.UserId = user.Id;
            if (id != category.Id) return BadRequest();

            if (!ModelState.IsValid)
            {
                ViewBag.AvailableIcons = GetIcons();
                return View(category);
            }

            _context.Update(category);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            var cat = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id && c.UserId == user.Id);
            if (cat != null)
            {
                var hasEvents = await _context.Events.AnyAsync(e => e.CategoryId == cat.Id);
                if (hasEvents)
                {
                    TempData["Error"] = "このカテゴリはイベントで使用されています。削除する前に再割り当てしてください。";
                    return RedirectToAction(nameof(Index));
                }

                _context.Categories.Remove(cat);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private static List<string> GetIcons() => new()
        {
            "fa-briefcase", "fa-people-group", "fa-house", "fa-bell", "fa-book-open",
            "fa-champagne-glasses", "fa-heart-pulse", "fa-plane", "fa-mug-hot", "fa-code",
            "fa-music", "fa-paintbrush", "fa-dumbbell", "fa-stethoscope", "fa-calendar-check"
        };
    }
}
