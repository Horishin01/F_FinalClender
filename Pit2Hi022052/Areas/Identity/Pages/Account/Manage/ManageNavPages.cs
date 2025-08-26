// .NET Foundation �ւ� 1 �ȏ�̌_��Ɋ�Â��ă��C�Z���X����Ă��܂��B
// ���̃t�@�C���� MIT ���C�Z���X�̉��Œ񋟂���܂��B
#nullable disable

using System;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Pit2Hi022052.Areas.Identity.Pages.Account.Manage
{
    /// <summary>
    /// ASP.NET Core Identity ���� UI �̓��������w���p�[�ł��B
    /// �A�v�������璼�ڎg�p���邱�Ƃ�z�肵�Ă��܂���i�����ύX�E�폜�����\��������܂��j�B
    /// </summary>
    public static class ManageNavPages
    {
        // ===== �y�[�W���iRazor Pages �̃t�@�C�����ƈ�v������j =====
        /// <summary>�v���t�B�[���iIndex�j</summary>
        public static string Index => "Index";

        /// <summary>���[���A�h���X�Ǘ�</summary>
        public static string Email => "Email";

        /// <summary>�p�X���[�h�ύX</summary>
        public static string ChangePassword => "ChangePassword";

        /// <summary>�l�f�[�^�̃_�E�����[�h</summary>
        public static string DownloadPersonalData => "DownloadPersonalData";

        /// <summary>�l�f�[�^�̍폜</summary>
        public static string DeletePersonalData => "DeletePersonalData";

        /// <summary>�O�����O�C��</summary>
        public static string ExternalLogins => "ExternalLogins";

        /// <summary>�l���</summary>
        public static string PersonalData => "PersonalData";

        /// <summary>��v�f�F��</summary>
        public static string TwoFactorAuthentication => "TwoFactorAuthentication";

        /// <summary>IC �J�[�h�Ǘ��i�g���j</summary>
        public static string ICCards => "ICCards";

        /// <summary>iCloud �ݒ�i�g���j</summary>
        public static string ICloudSetting => "ICloudSetting";

        // ===== �i�r�Q�[�V�����̃A�N�e�B�u����iactive �N���X�t�^�j =====
        /// <summary>Index �̃A�N�e�B�u����</summary>
        public static string IndexNavClass(ViewContext viewContext) => PageNavClass(viewContext, Index);

        /// <summary>Email �̃A�N�e�B�u����</summary>
        public static string EmailNavClass(ViewContext viewContext) => PageNavClass(viewContext, Email);

        /// <summary>ChangePassword �̃A�N�e�B�u����</summary>
        public static string ChangePasswordNavClass(ViewContext viewContext) => PageNavClass(viewContext, ChangePassword);

        /// <summary>DownloadPersonalData �̃A�N�e�B�u����</summary>
        public static string DownloadPersonalDataNavClass(ViewContext viewContext) => PageNavClass(viewContext, DownloadPersonalData);

        /// <summary>DeletePersonalData �̃A�N�e�B�u����</summary>
        public static string DeletePersonalDataNavClass(ViewContext viewContext) => PageNavClass(viewContext, DeletePersonalData);

        /// <summary>ExternalLogins �̃A�N�e�B�u����</summary>
        public static string ExternalLoginsNavClass(ViewContext viewContext) => PageNavClass(viewContext, ExternalLogins);

        /// <summary>PersonalData �̃A�N�e�B�u����</summary>
        public static string PersonalDataNavClass(ViewContext viewContext) => PageNavClass(viewContext, PersonalData);

        /// <summary>TwoFactorAuthentication �̃A�N�e�B�u����</summary>
        public static string TwoFactorAuthenticationNavClass(ViewContext viewContext) => PageNavClass(viewContext, TwoFactorAuthentication);

        /// <summary>ICCards �̃A�N�e�B�u����i�g���j</summary>
        public static string ICCardsNavClass(ViewContext viewContext) => PageNavClass(viewContext, ICCards);

        /// <summary>ICloudSetting �̃A�N�e�B�u����i�g���j</summary>
        public static string ICloudSettingNavClass(ViewContext viewContext) => PageNavClass(viewContext, ICloudSetting);

        /// <summary>
        /// ���ݕ\�����̃y�[�W���ƈ�v���邩�𔻒肵�A��v����ꍇ�� "active" ��Ԃ��܂��B
        /// ����ł͕\�����A�N�V������ DisplayName ����g���q�Ȃ��̃t�@�C�����𐄑����܂��B
        /// </summary>
        public static string PageNavClass(ViewContext viewContext, string page)
        {
            var activePage = viewContext.ViewData["ActivePage"] as string
                ?? System.IO.Path.GetFileNameWithoutExtension(viewContext.ActionDescriptor.DisplayName);
            return string.Equals(activePage, page, StringComparison.OrdinalIgnoreCase) ? "active" : null;
        }
    }
}
