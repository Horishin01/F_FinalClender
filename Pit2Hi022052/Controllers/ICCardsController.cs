using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Pit2Hi022052.Models;
using Pit2Hi022052.Data;
using System.Linq;
using PCSC;
using PCSC.Iso7816;
using Microsoft.AspNetCore.Authorization;

namespace Pit2Hi022052.Controllers
{
    public class ICCardsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ICCardsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ICカード一覧表示
        [Authorize(Roles = "admin,user")]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var card = await _context.ICCards.FirstOrDefaultAsync(c => c.UserId == currentUser.Id);
            return View(card); // UIDの表示
        }

        // ICカード登録
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            // ユーザーに既存のICカードが登録されていないか確認
            if (await _context.ICCards.AnyAsync(c => c.UserId == currentUser.Id))
            {
                ModelState.AddModelError("", "既にICカードが登録されています。");
                return RedirectToAction(nameof(Index));
            }

            // カードリーダーからUIDを取得
            string uid;
            try
            {
                uid = ReadCardUid();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"カードの読み取りに失敗しました: {ex.Message}");
                return RedirectToAction(nameof(Index));
            }

            // ICカードを登録
            var newCard = new ICCard
            {
                UserId = currentUser.Id,
                Uid = uid
            };

            _context.ICCards.Add(newCard);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ICカード削除
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var card = await _context.ICCards.FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

            if (card != null)
            {
                _context.ICCards.Remove(card);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // ICカード情報を編集
        [HttpPost]
        [Route("Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ICCard updatedCard)
        {
            if (!ModelState.IsValid)
            {
                ModelState.AddModelError("", "入力に誤りがあります。");
                return RedirectToAction(nameof(Index));
            }

            var currentUser = await _userManager.GetUserAsync(User);

            // 現在のユーザーのICカードを取得
            var card = await _context.ICCards.FirstOrDefaultAsync(c => c.UserId == currentUser.Id);

            if (card == null)
            {
                ModelState.AddModelError("", "ICカードが見つかりません。");
                return RedirectToAction(nameof(Index));
            }

            // 必要なプロパティを更新
            card.Uid = updatedCard.Uid;

            // 必要に応じて他のプロパティも更新
            // card.AnotherProperty = updatedCard.AnotherProperty;

            _context.ICCards.Update(card);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // カードリーダーからUIDを取得する処理
        private string ReadCardUid()
        {
            using (var context = ContextFactory.Instance.Establish(SCardScope.System))
            {
                var readerNames = context.GetReaders();
                if (readerNames.Length == 0)
                {
                    throw new Exception("カードリーダーが見つかりません。");
                }

                var reader = readerNames[0];
                using (var isoReader = new IsoReader(context, reader, SCardShareMode.Shared, SCardProtocol.Any, false))
                {
                    var apdu = new CommandApdu(IsoCase.Case2Short, isoReader.ActiveProtocol)
                    {
                        CLA = 0xFF, // クラス
                        INS = 0xCA, // 命令
                        P1 = 0x00,  // パラメータ1
                        P2 = 0x00,  // パラメータ2
                        Le = 0x00   // レスポンスの長さ
                    };

                    var response = isoReader.Transmit(apdu);
                    if (!response.HasData)
                    {
                        throw new Exception("UIDの取得に失敗しました。");
                    }

                    return BitConverter.ToString(response.GetData()).Replace("-", "");
                }
            }
        }
    }
}
