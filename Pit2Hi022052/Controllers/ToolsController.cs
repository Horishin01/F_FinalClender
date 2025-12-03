using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pit2Hi022052.Models;

namespace Pit2Hi022052.Controllers
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
