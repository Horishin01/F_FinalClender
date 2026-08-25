using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

namespace TimeLedger.Security;

internal sealed record WebTransportSecurity(bool RequireHttps, IPAddress? TrustedProxy)
{
    private const string ProductionHostPlaceholder = "__SET_PRODUCTION_HOST__";

    public static WebTransportSecurity Load(IConfiguration configuration, IHostEnvironment environment)
    {
        var requireHttps = configuration.GetValue<bool>("Security:RequireHttps");

        if (environment.IsProduction() && !requireHttps)
        {
            throw new InvalidOperationException(
                "TIMELEDGER-PRODUCTION-HTTPS-REQUIRED: ProductionではSecurity:RequireHttps=trueが必須です。");
        }

        if (environment.IsProduction())
        {
            ValidateProductionHosts(configuration["AllowedHosts"]);

            if (configuration.GetValue<bool>("BootstrapAdmin:Enabled"))
            {
                throw new InvalidOperationException(
                    "TIMELEDGER-PRODUCTION-BOOTSTRAP-ADMIN-DISALLOWED: Productionでは初期管理者の自動作成を有効にできません。");
            }
        }

        if (!requireHttps)
        {
            return new WebTransportSecurity(false, null);
        }

        var trustedProxyValue = configuration["Security:TrustedProxyIp"];
        if (!IPAddress.TryParse(trustedProxyValue, out var trustedProxy) || !IPAddress.IsLoopback(trustedProxy))
        {
            throw new InvalidOperationException(
                "TIMELEDGER-TRUSTED-PROXY-INVALID: Security:TrustedProxyIpには同一ホストのloopback IPを指定してください。");
        }

        return new WebTransportSecurity(true, trustedProxy);
    }

    public void ConfigureServices(IServiceCollection services)
    {
        if (!RequireHttps || TrustedProxy is null)
        {
            return;
        }

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
            options.KnownProxies.Add(TrustedProxy);
        });
    }

    public string Description => RequireHttps
        ? "Production HTTPS（同一ホストの信頼済みリバースプロキシ終端）"
        : "開発・テストHTTP";

    private static void ValidateProductionHosts(string? allowedHosts)
    {
        var hosts = (allowedHosts ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (hosts.Length == 0
            || hosts.Any(host => host == "*" || host.Contains(ProductionHostPlaceholder, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "TIMELEDGER-PRODUCTION-HOSTS-REQUIRED: ProductionのAllowedHostsを実際の証明書対象ホスト名へ設定してください。");
        }
    }
}
