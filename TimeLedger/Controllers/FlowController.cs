using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeLedger.Models;

namespace TimeLedger.Controllers
{
    [Authorize(Roles = RoleNames.Admin)]
    public class FlowController : Controller
    {
        public IActionResult Index()
        {
            ViewData["BodyClass"] = "flow-page";
            return View();
        }
    }
}
