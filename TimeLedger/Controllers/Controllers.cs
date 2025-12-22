/*----------------------------------------------------------
Controllers.cs
----------------------------------------------------------*/
// 役割: コントローラー共通のユーティリティ。例外チェーンを ModelState に追加するヘルパーを提供し、単体テストや再利用を簡素化する。
using Microsoft.AspNetCore.Mvc;
namespace TimeLedger
    .Controllers;
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
