using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Pit2Hi022052.Models;
using Pit2Hi022052.Services;
using Pit2Hi022052.ViewModels;

namespace Pit2Hi022052.Controllers
{
    [Authorize]
    public class BalanceSheetController : Controller
    {
        private readonly IBalanceSheetService _balanceSheetService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<BalanceSheetController> _logger;

        public BalanceSheetController(
            IBalanceSheetService balanceSheetService,
            UserManager<ApplicationUser> userManager,
            ILogger<BalanceSheetController> logger)
        {
            _balanceSheetService = balanceSheetService;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var vm = await BuildViewModelAsync();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add([Bind(Prefix = "EntryInput")] BalanceSheetEntryInputModel input)
        {
            if (!ModelState.IsValid)
            {
                var invalidVm = await BuildViewModelAsync(input);
                return View("Index", invalidVm);
            }

            var entry = new BalanceSheetEntry
            {
                Type = input.Type,
                Name = input.Name.Trim(),
                Tag = input.Tag,
                Amount = input.Amount,
                AsOfDate = DateTime.UtcNow.Date
            };

            await _balanceSheetService.AddEntryAsync(GetUserId(), entry);
            TempData["BsMessage"] = "行を追加しました。";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await _balanceSheetService.DeleteEntryAsync(GetUserId(), id);
            TempData["BsMessage"] = "行を削除しました。";
            return RedirectToAction(nameof(Index));
        }

        private async Task<BalanceSheetPageViewModel> BuildViewModelAsync(BalanceSheetEntryInputModel? input = null)
        {
            var summary = await _balanceSheetService.GetLatestSummaryAsync(GetUserId());
            return new BalanceSheetPageViewModel
            {
                Summary = summary,
                EntryInput = input ?? new BalanceSheetEntryInputModel()
            };
        }

        private string GetUserId()
        {
            var id = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(id))
            {
                _logger.LogWarning("BalanceSheetController: User ID not found.");
                throw new InvalidOperationException("認証情報が見つかりません。");
            }
            return id;
        }
    }
}
