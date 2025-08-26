using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PCSC;
using PCSC.Iso7816;
using Pit2Hi022052.Data;
using Pit2Hi022052.Models;

namespace Pit2Hi022052.Areas.Identity.Pages.Account.Manage
{
    public class ICCardsModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public ICCardsModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // 表示用：現在のカード（1ユーザー1枚想定）
        public ICCard? Card { get; private set; }

        // 画面右肩のステータス
        public string? StatusMessage { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Challenge();

            Card = await _db.ICCards.FirstOrDefaultAsync(x => x.UserId == user.Id);
            return Page();
        }

        /// <summary>
        /// カードを読み取り、存在すれば上書き・なければ新規登録。
        /// ※ 手入力パラメータは一切受け取らない（仕様固定）
        /// </summary>
        public async Task<IActionResult> OnPostReadAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Challenge();

            try
            {
                var uid = ReadCardUid();

                // 他ユーザーが同じUIDを持っていないか軽く確認（必要に応じてユニーク制約も併用）
                var duplicated = await _db.ICCards
                    .AnyAsync(x => x.Uid == uid && x.UserId != user.Id);
                if (duplicated)
                {
                    StatusMessage = "このUIDは他のユーザーに登録済みです。";
                    await LoadCardAsync(user.Id);
                    return Page();
                }

                var card = await _db.ICCards.FirstOrDefaultAsync(x => x.UserId == user.Id);
                if (card is null)
                {
                    // 新規
                    card = new ICCard
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        UserId = user.Id,
                        Uid = uid
                    };
                    _db.ICCards.Add(card);
                    StatusMessage = "カードを登録しました。";
                }
                else
                {
                    // 上書き
                    card.Uid = uid;
                    _db.ICCards.Update(card);
                    StatusMessage = "カード情報を読み取りで上書きしました。";
                }

                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"読み取りに失敗しました: {ex.Message}";
            }

            await LoadCardAsync(user.Id);
            return Page();
        }

        /// <summary>削除のみ許可（UIDの手入力更新は不可）</summary>
        public async Task<IActionResult> OnPostDeleteAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Challenge();

            var card = await _db.ICCards.FirstOrDefaultAsync(x => x.UserId == user.Id);
            if (card is not null)
            {
                _db.ICCards.Remove(card);
                await _db.SaveChangesAsync();
                StatusMessage = "カードを削除しました。";
            }

            await LoadCardAsync(user.Id);
            return Page();
        }

        // ===== ヘルパ =====
        private async Task LoadCardAsync(string userId)
            => Card = await _db.ICCards.FirstOrDefaultAsync(x => x.UserId == userId);

        /// <summary>PC/SCでUIDを取得（環境に合わせてAPDUは必要に応じて調整）</summary>
        private static string ReadCardUid()
        {
            using var ctx = ContextFactory.Instance.Establish(SCardScope.System);
            var readers = ctx.GetReaders();
            if (readers is null || readers.Length == 0)
                throw new InvalidOperationException("カードリーダーが見つかりません。");

            using var iso = new IsoReader(ctx, readers[0], SCardShareMode.Shared, SCardProtocol.Any, false);

            // 代表的なUID取得APDU（要環境確認）
            var apdu = new CommandApdu(IsoCase.Case2Short, iso.ActiveProtocol)
            {
                CLA = 0xFF,
                INS = 0xCA,
                P1 = 0x00,
                P2 = 0x00,
                Le = 0x00
            };

            var resp = iso.Transmit(apdu);
            if (!resp.HasData)
                throw new InvalidOperationException("UIDの取得に失敗しました。");

            return BitConverter.ToString(resp.GetData()).Replace("-", "");
        }
    }
}
