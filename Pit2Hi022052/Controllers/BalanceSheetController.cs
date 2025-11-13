using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pit2Hi022052.Services;
using Pit2Hi022052.ViewModels;
using System.Linq;

namespace Pit2Hi022052.Controllers
{
    [Authorize]
    public class BalanceSheetController : Controller
    {
        public IActionResult Index()
        {
            var seed = BalanceSheetSampleProvider.GetSeed();
            var model = new BalanceSheetViewModel
            {
                Assets = seed.Where(x => x.Category == Models.BalanceSheetCategory.Asset).ToList(),
                Liabilities = seed.Where(x => x.Category == Models.BalanceSheetCategory.Liability).ToList()
            };
            return View(model);
        }
    }
}
