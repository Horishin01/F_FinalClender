using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pit2Hi022052.Models;

namespace Pit2Hi022052.Controllers
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
