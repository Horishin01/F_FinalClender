using System;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace TimeLedger.Areas.Identity.Pages.Account.Manage
{
    public static class ManageNavPages
    {
        public static string Index => "Index";
        public static string Email => "Email";
        public static string ChangePassword => "ChangePassword";
        public static string DownloadPersonalData => "DownloadPersonalData";
        public static string DeletePersonalData => "DeletePersonalData";
        public static string ExternalLogins => "ExternalLogins";
        public static string PersonalData => "PersonalData";
        public static string TwoFactorAuthentication => "TwoFactorAuthentication";
        public static string ICCards => "ICCards";
        public static string ICloudSetting => "ICloudSetting";
        public static string ExternalCalendarsOutlook => "ExternalCalendarsOutlook";
        public static string ExternalCalendarsGoogle => "ExternalCalendarsGoogle";

        public static string IndexNavClass(ViewContext viewContext) => PageNavClass(viewContext, Index);
        public static string EmailNavClass(ViewContext viewContext) => PageNavClass(viewContext, Email);
        public static string ChangePasswordNavClass(ViewContext viewContext) => PageNavClass(viewContext, ChangePassword);
        public static string DownloadPersonalDataNavClass(ViewContext viewContext) => PageNavClass(viewContext, DownloadPersonalData);
        public static string DeletePersonalDataNavClass(ViewContext viewContext) => PageNavClass(viewContext, DeletePersonalData);
        public static string ExternalLoginsNavClass(ViewContext viewContext) => PageNavClass(viewContext, ExternalLogins);
        public static string PersonalDataNavClass(ViewContext viewContext) => PageNavClass(viewContext, PersonalData);
        public static string TwoFactorAuthenticationNavClass(ViewContext viewContext) => PageNavClass(viewContext, TwoFactorAuthentication);
        public static string ICCardsNavClass(ViewContext viewContext) => PageNavClass(viewContext, ICCards);
        public static string ICloudSettingNavClass(ViewContext viewContext) => PageNavClass(viewContext, ICloudSetting);
        public static string ExternalCalendarsOutlookNavClass(ViewContext viewContext) => PageNavClass(viewContext, ExternalCalendarsOutlook);
        public static string ExternalCalendarsGoogleNavClass(ViewContext viewContext) => PageNavClass(viewContext, ExternalCalendarsGoogle);

        public static string PageNavClass(ViewContext viewContext, string page)
        {
            var activePage = viewContext.ViewData["ActivePage"] as string
                ?? System.IO.Path.GetFileNameWithoutExtension(viewContext.ActionDescriptor.DisplayName);
            return string.Equals(activePage, page, StringComparison.OrdinalIgnoreCase) ? "active" : string.Empty;
        }
    }
}
