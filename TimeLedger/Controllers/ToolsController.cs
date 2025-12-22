// ToolsController
// 管理者向けのツール系デモページ（Packlist/TimeInsight/Patterns）の表示のみを担当。

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeLedger.Models;

namespace TimeLedger.Controllers
{
    [Authorize(Roles = RoleNames.Admin)]
    public class ToolsController : Controller
    {
        public IActionResult Packlist()
        {
            return View();
        }

        public IActionResult TimeInsight()
        {
            return View();
        }

        public IActionResult Patterns()
        {
            return View();
        }
    }
}
