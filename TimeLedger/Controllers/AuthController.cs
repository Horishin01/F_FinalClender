using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TimeLedger.Extensions;
using TimeLedger.Models;
using TimeLedger.Services;

namespace TimeLedger.Controllers
{
    [Authorize]
    [Route("auth")]
    public class AuthController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IOutlookCalendarService _outlookService;
        private readonly IGoogleCalendarService _googleService;
        private readonly IAuthenticationSchemeProvider _schemeProvider;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            IOutlookCalendarService outlookService,
            IGoogleCalendarService googleService,
            IAuthenticationSchemeProvider schemeProvider,
            ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _outlookService = outlookService;
            _googleService = googleService;
            _schemeProvider = schemeProvider;
            _logger = logger;
        }

        [HttpGet("outlook/connect")]
        public async Task<IActionResult> OutlookConnect(string? returnUrl = null)
        {
            if (await _schemeProvider.GetSchemeAsync(CalendarAuthDefaults.OutlookScheme) == null)
            {
                TempData["StatusMessage"] = "Outlook 連携のアプリ設定が未構成です。システム管理者にお問い合わせください。";
                return RedirectToOutlook(returnUrl);
            }

            var redirectUri = Url.Action(nameof(OutlookCallback), "Auth", new { returnUrl }, Request.Scheme);
            var props = new AuthenticationProperties
            {
                RedirectUri = redirectUri
            };
            props.SetParameter("prompt", "consent");
            return Challenge(props, CalendarAuthDefaults.OutlookScheme);
        }

        [HttpGet("outlook/callback")]
        public async Task<IActionResult> OutlookCallback(string? returnUrl = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var result = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
            if (!result.Succeeded)
            {
                TempData["StatusMessage"] = "Outlook 連携に失敗しました。もう一度お試しください。";
                return RedirectToOutlook(returnUrl);
            }

            var accessToken = result.Properties?.GetTokenValue("access_token");
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                TempData["StatusMessage"] = "Outlook のアクセストークンを取得できませんでした。";
                return RedirectToOutlook(returnUrl);
            }

            var refreshToken = result.Properties?.GetTokenValue("refresh_token");
            var expiresAtRaw = result.Properties?.GetTokenValue("expires_at");
            var scope = result.Properties?.GetTokenValue("scope") ?? string.Join(" ", CalendarAuthDefaults.OutlookScopes);
            var accountEmail = GetAccountEmail(result.Principal);

            await _outlookService.SaveTokensAsync(user.Id, accountEmail, accessToken, refreshToken, ParseExpires(expiresAtRaw), scope);
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            TempData["StatusMessage"] = "Outlook と連携しました。";
            return RedirectToOutlook(returnUrl);
        }

        [HttpGet("google/calendar/connect")]
        public async Task<IActionResult> GoogleConnect(string? returnUrl = null)
        {
            if (await _schemeProvider.GetSchemeAsync(CalendarAuthDefaults.GoogleScheme) == null)
            {
                TempData["StatusMessage"] = "Google 連携のアプリ設定が未構成です。システム管理者にお問い合わせください。";
                return RedirectToGoogle(returnUrl);
            }

            var redirectUri = Url.Action(nameof(GoogleCallback), "Auth", new { returnUrl }, Request.Scheme);
            var props = new AuthenticationProperties
            {
                RedirectUri = redirectUri
            };
            props.SetParameter("prompt", "consent");
            props.SetParameter("access_type", "offline");
            props.SetParameter("include_granted_scopes", "true");
            return Challenge(props, CalendarAuthDefaults.GoogleScheme);
        }

        [HttpGet("google/calendar/callback")]
        public async Task<IActionResult> GoogleCallback(string? returnUrl = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var result = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
            if (!result.Succeeded)
            {
                TempData["StatusMessage"] = "Google 連携に失敗しました。もう一度お試しください。";
                return RedirectToGoogle(returnUrl);
            }

            var accessToken = result.Properties?.GetTokenValue("access_token");
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                TempData["StatusMessage"] = "Google のアクセストークンを取得できませんでした。";
                return RedirectToGoogle(returnUrl);
            }

            var refreshToken = result.Properties?.GetTokenValue("refresh_token");
            var expiresAtRaw = result.Properties?.GetTokenValue("expires_at");
            var scope = result.Properties?.GetTokenValue("scope") ?? string.Join(" ", CalendarAuthDefaults.GoogleScopes);
            var accountEmail = GetAccountEmail(result.Principal);

            await _googleService.SaveTokensAsync(user.Id, accountEmail, accessToken, refreshToken, ParseExpires(expiresAtRaw), scope);
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            TempData["StatusMessage"] = "Google カレンダーと連携しました。";
            return RedirectToGoogle(returnUrl);
        }

        private IActionResult RedirectToOutlook(string? returnUrl)
        {
            var manageUrl = Url.Page("/Account/Manage/ExternalCalendarsOutlook", null, new { area = "Identity" }, Request.Scheme);
            return RedirectToLocalOrDefault(returnUrl, manageUrl);
        }

        private IActionResult RedirectToGoogle(string? returnUrl)
        {
            var manageUrl = Url.Page("/Account/Manage/ExternalCalendarsGoogle", null, new { area = "Identity" }, Request.Scheme);
            return RedirectToLocalOrDefault(returnUrl, manageUrl);
        }

        private IActionResult RedirectToLocalOrDefault(string? returnUrl, string? defaultUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return Redirect(defaultUrl ?? "/");
        }

        private static DateTime? ParseExpires(string? expiresAtRaw)
        {
            if (string.IsNullOrWhiteSpace(expiresAtRaw)) return null;
            if (DateTimeOffset.TryParse(expiresAtRaw, out var dto))
            {
                return dto.UtcDateTime;
            }
            return null;
        }

        private static string? GetAccountEmail(ClaimsPrincipal? principal)
        {
            return principal?.FindFirstValue(ClaimTypes.Email)
                   ?? principal?.FindFirstValue("preferred_username")
                   ?? principal?.FindFirstValue("upn")
                   ?? principal?.Identity?.Name;
        }
    }
}
