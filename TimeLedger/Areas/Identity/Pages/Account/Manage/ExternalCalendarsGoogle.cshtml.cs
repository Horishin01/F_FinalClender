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
    public class ExternalCalendarsGoogleModel : PageModel
    {
        private readonly IGoogleCalendarService _googleCalendarService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;

        public ExternalCalendarsGoogleModel(
            IGoogleCalendarService googleCalendarService,
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration)
        {
            _googleCalendarService = googleCalendarService;
            _userManager = userManager;
            _configuration = configuration;
        }

        public ConnectionViewModel Connection { get; private set; } = ConnectionViewModel.Empty;

        public string DefaultScope => string.Join(" ", CalendarAuthDefaults.GoogleScopes);

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

            await _googleCalendarService.RemoveConnectionAsync(user.Id);
            StatusMessage = "Google カレンダーの連携を解除しました。";
            return RedirectToPage();
        }

        private bool HasOAuthConfig()
        {
            var clientId = _configuration["Authentication:Google:ClientId"];
            var clientSecret = _configuration["Authentication:Google:ClientSecret"];
            return !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret);
        }

        private async Task LoadAsync(string userId)
        {
            var connection = await _googleCalendarService.GetConnectionAsync(userId);
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

            public static ConnectionViewModel From(GoogleCalendarConnection? connection)
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
