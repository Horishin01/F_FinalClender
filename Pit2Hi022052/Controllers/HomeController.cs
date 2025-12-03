using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pit2Hi022052.Data;
using Pit2Hi022052.Models;
using Pit2Hi022052.ViewModels;
using System.Diagnostics;
using System.Linq;

namespace Pit2Hi022052.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(
            ILogger<HomeController> logger,
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return View(new HomeIndexViewModel { IsAuthenticated = false });
            }

            var events = await _db.Events
                .Where(e => e.UserId == currentUser.Id)
                .OrderBy(e => e.StartDate ?? DateTime.MinValue)
                .Take(250)
                .ToListAsync();

            var model = new HomeIndexViewModel
            {
                IsAuthenticated = true,
                Events = events.Select(e => new CalendarPreviewEvent
                {
                    Id = e.Id,
                    Title = e.Title,
                    Start = e.StartDate,
                    End = e.EndDate,
                    Description = e.Description,
                    AllDay = e.AllDay
                }).ToList()
            };

            return View(model);
        }

        public async Task<IActionResult> Privacy()
        {
            var updates = await _db.AppNotices
                .AsNoTracking()
                .Where(n => n.Kind == NoticeKind.Update)
                .OrderByDescending(n => n.OccurredAt)
                .ThenByDescending(n => n.CreatedAtUtc)
                .ToListAsync();

            var incidents = await _db.AppNotices
                .AsNoTracking()
                .Where(n => n.Kind == NoticeKind.Incident)
                .OrderByDescending(n => n.OccurredAt)
                .ThenByDescending(n => n.CreatedAtUtc)
                .ToListAsync();

            var model = new PrivacyViewModel
            {
                Updates = updates,
                Incidents = incidents
            };

            return View(model);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
