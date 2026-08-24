[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectDirectory 'TimeLedger.csproj'

& dotnet build $projectFile -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "TimeLedgerの$Configurationビルドに失敗しました。"
}

$assembly = Join-Path $projectDirectory "bin\$Configuration\net8.0\TimeLedger.dll"
if (-not (Test-Path -LiteralPath $assembly -PathType Leaf)) {
    throw '検査対象のTimeLedger.dllが見つかりません。'
}

$cases = @(
    @{ Name = 'development-http'; Argument = '--validate-web-security'; Environment = 'Development'; Overrides = @{}; ExpectedExit = 0; Token = '開発・テストHTTP' },
    @{ Name = 'production-valid-proxy'; Argument = '--validate-web-security'; Environment = 'Production'; Overrides = @{ 'AllowedHosts' = 'calendar.example.test' }; ExpectedExit = 0; Token = 'Production HTTPS' },
    @{ Name = 'production-host-placeholder'; Argument = '--validate-web-security'; Environment = 'Production'; Overrides = @{}; ExpectedExit = 2; Token = 'TIMELEDGER-PRODUCTION-HOSTS-REQUIRED' },
    @{ Name = 'production-https-disabled'; Argument = '--validate-web-security'; Environment = 'Production'; Overrides = @{ 'AllowedHosts' = 'calendar.example.test'; 'Security__RequireHttps' = 'false' }; ExpectedExit = 2; Token = 'TIMELEDGER-PRODUCTION-HTTPS-REQUIRED' },
    @{ Name = 'production-untrusted-proxy'; Argument = '--validate-web-security'; Environment = 'Production'; Overrides = @{ 'AllowedHosts' = 'calendar.example.test'; 'Security__TrustedProxyIp' = '10.0.0.1' }; ExpectedExit = 2; Token = 'TIMELEDGER-TRUSTED-PROXY-INVALID' },
    @{ Name = 'production-bootstrap-admin'; Argument = '--validate-web-security'; Environment = 'Production'; Overrides = @{ 'AllowedHosts' = 'calendar.example.test'; 'BootstrapAdmin__Enabled' = 'true' }; ExpectedExit = 2; Token = 'TIMELEDGER-PRODUCTION-BOOTSTRAP-ADMIN-DISALLOWED' },
    @{ Name = 'development-connection-missing'; Argument = '--validate-db-configuration'; Environment = 'Development'; Overrides = @{ 'ConnectionStrings__DefaultConnection' = '' }; ExpectedExit = 2; Token = 'TIMELEDGER-DEVELOPMENT-DB-CONNECTION-MISSING' },
    @{ Name = 'development-connection-configured'; Argument = '--validate-db-configuration'; Environment = 'Development'; Overrides = @{ 'ConnectionStrings__DefaultConnection' = 'Host=invalid.example;Database=TimeLedger;Username=validation;Password=not-used' }; ExpectedExit = 0; Token = 'DefaultConnection設定済み（Development）' },
    @{ Name = 'production-connection-missing'; Argument = '--validate-db-configuration'; Environment = 'Production'; Overrides = @{ 'AllowedHosts' = 'calendar.example.test'; 'ConnectionStrings__DefaultConnection' = '' }; ExpectedExit = 2; Token = 'TIMELEDGER-PRODUCTION-DB-CONNECTION-MISSING' },
    @{ Name = 'production-connection-configured'; Argument = '--validate-db-configuration'; Environment = 'Production'; Overrides = @{ 'AllowedHosts' = 'calendar.example.test'; 'ConnectionStrings__DefaultConnection' = 'Host=invalid.example;Database=TimeLedger;Username=validation;Password=not-used' }; ExpectedExit = 0; Token = 'DefaultConnection設定済み（Production）' }
)

$environmentKeys = @(
    'AllowedHosts',
    'Security__RequireHttps',
    'Security__TrustedProxyIp',
    'BootstrapAdmin__Enabled',
    'ConnectionStrings__DefaultConnection'
)

$failures = @()
foreach ($case in $cases) {
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = 'dotnet'
    $startInfo.WorkingDirectory = $projectDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.ArgumentList.Add($assembly)
    $startInfo.ArgumentList.Add($case.Argument)
    $startInfo.Environment['ASPNETCORE_ENVIRONMENT'] = $case.Environment

    foreach ($key in $environmentKeys) {
        $null = $startInfo.Environment.Remove($key)
    }
    foreach ($entry in $case.Overrides.GetEnumerator()) {
        $startInfo.Environment[$entry.Key] = $entry.Value
    }

    $process = [System.Diagnostics.Process]::Start($startInfo)
    $standardOutput = $process.StandardOutput.ReadToEnd()
    $standardError = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    $output = $standardOutput + $standardError
    if ($process.ExitCode -ne $case.ExpectedExit -or -not $output.Contains($case.Token)) {
        $failures += [pscustomobject]@{
            Name = $case.Name
            ExitCode = $process.ExitCode
            ExpectedExit = $case.ExpectedExit
            ExpectedMessageFound = $output.Contains($case.Token)
        }
    }
}

if ($failures.Count -gt 0) {
    $failures | Format-Table -AutoSize | Out-String | Write-Error
    throw "Web通信構成確認に$($failures.Count)件失敗しました。"
}

Write-Host "Web通信構成確認: $($cases.Count)/$($cases.Count)件成功（DB接続・サーバー起動なし）"
