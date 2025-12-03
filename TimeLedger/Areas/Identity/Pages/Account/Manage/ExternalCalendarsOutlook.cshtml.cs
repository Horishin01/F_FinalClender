using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TimeLedger.Extensions;
using TimeLedger.Models;
using TimeLedger.Services;

namespace TimeLedger.Areas.Identity.Pages.Account.Manage
{
    [Authorize]
    public class ExternalCalendarsOutlookModel : PageModel
    {
        private readonly IOutlookCalendarService _outlookCalendarService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;

        public ExternalCalendarsOutlookModel(
            IOutlookCalendarService outlookCalendarService,
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration)
        {
            _outlookCalendarService = outlookCalendarService;
            _userManager = userManager;
            _configuration = configuration;
        }

        public ConnectionViewModel Connection { get; private set; } = ConnectionViewModel.Empty;

        public string DefaultScope => string.Join(" ", CalendarAuthDefaults.OutlookScopes);

        public bool IsOAuthConfigured { get; private set; }

        [TempData]
        public string? StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Challenge();

            IsOAuthConfigured = HasOAuthConfig();
            await LoadAsync(user.Id);
            return Page();
        }

        public async Task<IActionResult> OnPostDisconnectAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Challenge();

            await _outlookCalendarService.RemoveConnectionAsync(user.Id);
            StatusMessage = "Outlook の連携を解除しました。";
            return RedirectToPage();
        }

        private bool HasOAuthConfig()
        {
            var clientId = _configuration["Authentication:Outlook:ClientId"];
            var clientSecret = _configuration["Authentication:Outlook:ClientSecret"];
            return !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret);
        }

        private async Task LoadAsync(string userId)
        {
            var connection = await _outlookCalendarService.GetConnectionAsync(userId);
            Connection = ConnectionViewModel.From(connection);
        }

        public class ConnectionViewModel
        {
            public static readonly ConnectionViewModel Empty = new();

            public bool IsConnected { get; init; }
            public bool HasError { get; init; }
            public string StatusText { get; init; } = "未連携";
            public string? AccountEmail { get; init; }
            public string? Scope { get; init; }
            public DateTime? ExpiresAtUtc { get; init; }
            public DateTime? LastSyncedAtUtc { get; init; }

            public static ConnectionViewModel From(OutlookCalendarConnection? connection)
            {
                if (connection == null)
                {
                    return Empty;
                }

                var expired = connection.ExpiresAtUtc.HasValue && connection.ExpiresAtUtc.Value <= DateTime.UtcNow;
                return new ConnectionViewModel
                {
                    IsConnected = true,
                    HasError = expired,
                    StatusText = expired ? "エラー（再認証が必要です）" : "連携中",
                    AccountEmail = connection.AccountEmail,
                    Scope = connection.Scope,
                    ExpiresAtUtc = connection.ExpiresAtUtc,
                    LastSyncedAtUtc = connection.LastSyncedAtUtc
                };
            }
        }
    }
}
