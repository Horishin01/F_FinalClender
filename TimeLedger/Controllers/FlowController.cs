// FlowController
// Flow デモページの表示のみを行うシンプルなコントローラー。BodyClass を設定してビューを返す。

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TimeLedger.Controllers
{
    [AllowAnonymous]
    public class FlowController : Controller
    {
        public IActionResult Index()
        {
            ViewData["BodyClass"] = "flow-page";
            return View();
        }
    }
}
