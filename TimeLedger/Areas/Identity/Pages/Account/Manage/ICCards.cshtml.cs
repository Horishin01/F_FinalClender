using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PCSC;
using PCSC.Exceptions;
using PCSC.Iso7816;
using TimeLedger.Data;
using TimeLedger.Models;

namespace TimeLedger.Areas.Identity.Pages.Account.Manage
{
    public class ICCardsModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private DbSet<ICCard> CardSet => _db.ICCards ?? throw new InvalidOperationException("ICCards DbSet is not configured.");

        public ICCardsModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // 表示用：現在のカード（1ユーザー1枚想定）
        public ICCard? Card { get; private set; }

        // 画面右肩のステータス
        public string? StatusMessage { get; private set; }
        public bool IsReadOnly { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!AlphaFeatureFlags.AccountAlphaFeatures) return NotFound();
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Challenge();

            IsReadOnly = !await IsAdminAsync(user);
            Card = await CardSet.FirstOrDefaultAsync(x => x.UserId == user.Id);
            return Page();
        }

        /// <summary>
        /// カードを読み取り、存在すれば上書き・なければ新規登録。
        /// ※ 手入力パラメータは一切受け取らない（仕様固定）
        /// </summary>
        public async Task<IActionResult> OnPostReadAsync()
        {
            if (!AlphaFeatureFlags.AccountAlphaFeatures) return NotFound();
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Challenge();
            if (!await IsAdminAsync(user)) return Forbid();

            try
            {
                var uid = ReadCardUid();

                // 他ユーザーが同じUIDを持っていないか軽く確認（必要に応じてユニーク制約も併用）
                var duplicated = await CardSet
                    .AnyAsync(x => x.Uid == uid && x.UserId != user.Id);
                if (duplicated)
                {
                    StatusMessage = "このUIDは他のユーザーに登録済みです。";
                    await LoadCardAsync(user.Id);
                    return Page();
                }

                var card = await CardSet.FirstOrDefaultAsync(x => x.UserId == user.Id);
                if (card is null)
                {
                    // 新規
                    card = new ICCard
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        UserId = user.Id,
                        Uid = uid
                    };
                    CardSet.Add(card);
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
            catch (DllNotFoundException)
            {
                StatusMessage = BuildPcscMissingMessage();
            }
            catch (TypeInitializationException ex) when (ex.InnerException is DllNotFoundException)
            {
                StatusMessage = BuildPcscMissingMessage();
            }
            catch (NoReadersAvailableException)
            {
                StatusMessage = "カードリーダーが見つかりません。接続状態と pcscd の起動状態を確認してください。";
            }
            catch (NoServiceException)
            {
                StatusMessage = "PC/SC サービスが利用できません。pcscd が起動しているか確認してください。";
            }
            catch (PCSCException ex)
            {
                StatusMessage = $"カード読み取りエラー: {ex.Message}";
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
            if (!AlphaFeatureFlags.AccountAlphaFeatures) return NotFound();
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Challenge();
            if (!await IsAdminAsync(user)) return Forbid();

            var card = await CardSet.FirstOrDefaultAsync(x => x.UserId == user.Id);
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
            => Card = await CardSet.FirstOrDefaultAsync(x => x.UserId == userId);

        private Task<bool> IsAdminAsync(ApplicationUser user)
            => _userManager.IsInRoleAsync(user, RoleNames.Admin);

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

        private static string BuildPcscMissingMessage()
            => "PC/SC ライブラリ (libpcsclite.so.1) が見つかりません。サーバーに libpcsclite1 / pcscd をインストールしてから再実行してください。";
    }
}
