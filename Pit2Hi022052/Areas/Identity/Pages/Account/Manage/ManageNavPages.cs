// .NET Foundation への 1 つ以上の契約に基づいてライセンスされています。
// このファイルは MIT ライセンスの下で提供されます。
#nullable disable

using System;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Pit2Hi022052.Areas.Identity.Pages.Account.Manage
{
    /// <summary>
    /// ASP.NET Core Identity 既定 UI の内部向けヘルパーです。
    /// アプリ側から直接使用することを想定していません（将来変更・削除される可能性があります）。
    /// </summary>
    public static class ManageNavPages
    {
        // ===== ページ名（Razor Pages のファイル名と一致させる） =====
        /// <summary>プロフィール（Index）</summary>
        public static string Index => "Index";

        /// <summary>メールアドレス管理</summary>
        public static string Email => "Email";

        /// <summary>パスワード変更</summary>
        public static string ChangePassword => "ChangePassword";

        /// <summary>個人データのダウンロード</summary>
        public static string DownloadPersonalData => "DownloadPersonalData";

        /// <summary>個人データの削除</summary>
        public static string DeletePersonalData => "DeletePersonalData";

        /// <summary>外部ログイン</summary>
        public static string ExternalLogins => "ExternalLogins";

        /// <summary>個人情報</summary>
        public static string PersonalData => "PersonalData";

        /// <summary>二要素認証</summary>
        public static string TwoFactorAuthentication => "TwoFactorAuthentication";

        /// <summary>IC カード管理（拡張）</summary>
        public static string ICCards => "ICCards";

        /// <summary>iCloud 設定（拡張）</summary>
        public static string ICloudSetting => "ICloudSetting";

        // ===== ナビゲーションのアクティブ判定（active クラス付与） =====
        /// <summary>Index のアクティブ判定</summary>
        public static string IndexNavClass(ViewContext viewContext) => PageNavClass(viewContext, Index);

        /// <summary>Email のアクティブ判定</summary>
        public static string EmailNavClass(ViewContext viewContext) => PageNavClass(viewContext, Email);

        /// <summary>ChangePassword のアクティブ判定</summary>
        public static string ChangePasswordNavClass(ViewContext viewContext) => PageNavClass(viewContext, ChangePassword);

        /// <summary>DownloadPersonalData のアクティブ判定</summary>
        public static string DownloadPersonalDataNavClass(ViewContext viewContext) => PageNavClass(viewContext, DownloadPersonalData);

        /// <summary>DeletePersonalData のアクティブ判定</summary>
        public static string DeletePersonalDataNavClass(ViewContext viewContext) => PageNavClass(viewContext, DeletePersonalData);

        /// <summary>ExternalLogins のアクティブ判定</summary>
        public static string ExternalLoginsNavClass(ViewContext viewContext) => PageNavClass(viewContext, ExternalLogins);

        /// <summary>PersonalData のアクティブ判定</summary>
        public static string PersonalDataNavClass(ViewContext viewContext) => PageNavClass(viewContext, PersonalData);

        /// <summary>TwoFactorAuthentication のアクティブ判定</summary>
        public static string TwoFactorAuthenticationNavClass(ViewContext viewContext) => PageNavClass(viewContext, TwoFactorAuthentication);

        /// <summary>ICCards のアクティブ判定（拡張）</summary>
        public static string ICCardsNavClass(ViewContext viewContext) => PageNavClass(viewContext, ICCards);

        /// <summary>ICloudSetting のアクティブ判定（拡張）</summary>
        public static string ICloudSettingNavClass(ViewContext viewContext) => PageNavClass(viewContext, ICloudSetting);

        /// <summary>
        /// 現在表示中のページ名と一致するかを判定し、一致する場合は "active" を返します。
        /// 既定では表示中アクションの DisplayName から拡張子なしのファイル名を推測します。
        /// </summary>
        public static string PageNavClass(ViewContext viewContext, string page)
        {
            var activePage = viewContext.ViewData["ActivePage"] as string
                ?? System.IO.Path.GetFileNameWithoutExtension(viewContext.ActionDescriptor.DisplayName);
            return string.Equals(activePage, page, StringComparison.OrdinalIgnoreCase) ? "active" : null;
        }
    }
}
