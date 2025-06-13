/*----------------------------------------------------------
Controllers.cs
----------------------------------------------------------*/
using Microsoft.AspNetCore.Mvc;
namespace Pit2Hi022999.Controllers;
//==========================================================
// Controllers クラス
[NonController]
public static class Controllers
{
    //--------
    // 例外に対する処理
    [NonAction]
    public static void AddAllExceptionMessagesToModelError
(Controller controller, Exception? e)
    {
        while (e is not null)
        {
            controller.ModelState.AddModelError(string.Empty, e.Message);
            e = e.InnerException;
        }
        return;
    }
    //--------
    // END
    //--------
}
//==========================================================
// END
//==========================================================