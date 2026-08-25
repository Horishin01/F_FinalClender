[CmdletBinding()]
param(
    [switch]$PrepareOnly
)

$ErrorActionPreference = 'Stop'
$databaseDirectory = Split-Path -Parent $PSScriptRoot
$projectDirectory = Split-Path -Parent $databaseDirectory
$localSettingsFile = Join-Path $projectDirectory 'appsettings.Development.Local.json'
$configFile = Join-Path $databaseDirectory 'config\development.env'
$composeFile = Join-Path $databaseDirectory 'compose.development.yaml'
$backupDirectory = Join-Path $databaseDirectory 'runtime\development\backups'
$dataDirectory = Join-Path $databaseDirectory 'runtime\development\data'

function Get-ConnectionValue {
    param(
        [System.Data.Common.DbConnectionStringBuilder]$Builder,
        [string[]]$Names,
        [switch]$Required
    )

    foreach ($name in $Names) {
        if ($Builder.ContainsKey($name)) {
            return [string]$Builder[$name]
        }
    }

    if ($Required) {
        throw "接続文字列に $($Names -join '/') がありません。"
    }

    return $null
}

function Invoke-DockerCompose {
    param([string[]]$Arguments)

    & docker compose --env-file $configFile -f $composeFile @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose $($Arguments -join ' ') に失敗しました。接続先のLocal設定は変更していません。"
    }
}

if (-not (Test-Path -LiteralPath $localSettingsFile -PathType Leaf)) {
    throw "$localSettingsFile がありません。現在の移行元接続文字列を先に設定してください。"
}

$localSettings = Get-Content -LiteralPath $localSettingsFile -Raw | ConvertFrom-Json
$sourceConnectionString = $localSettings.ConnectionStrings.DefaultConnection
if ([string]::IsNullOrWhiteSpace($sourceConnectionString)) {
    throw 'appsettings.Development.Local.json に移行元のDefaultConnectionがありません。'
}

$source = [System.Data.Common.DbConnectionStringBuilder]::new()
$source.set_ConnectionString($sourceConnectionString)
$sourceHost = Get-ConnectionValue -Builder $source -Names @('Host', 'Server') -Required
$sourcePort = Get-ConnectionValue -Builder $source -Names @('Port')
if ([string]::IsNullOrWhiteSpace($sourcePort)) { $sourcePort = '5432' }
$sourceDatabase = Get-ConnectionValue -Builder $source -Names @('Database', 'Initial Catalog') -Required
$sourceUsername = Get-ConnectionValue -Builder $source -Names @('Username', 'User ID', 'UserId') -Required
$sourcePassword = Get-ConnectionValue -Builder $source -Names @('Password', 'Pwd') -Required

if (($sourceHost -in @('127.0.0.1', 'localhost', '::1')) -and $sourcePort -eq '55432') {
    throw '開発設定はすでにプロジェクト配下DBを参照しています。再移行は行いません。'
}
if ($sourceDatabase -notmatch '^[A-Za-z0-9_]+$' -or $sourceUsername -notmatch '^[A-Za-z0-9_]+$') {
    throw 'DB名またはユーザー名にenvファイルへ安全に自動転記できない文字があります。手動移行してください。'
}

if (-not (Test-Path -LiteralPath $configFile -PathType Leaf)) {
    $randomBytes = [byte[]]::new(24)
    $random = [Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $random.GetBytes($randomBytes)
    }
    finally {
        $random.Dispose()
    }
    $targetPassword = -join ($randomBytes | ForEach-Object { $_.ToString('x2') })
    $targetConnectionString = "Host=127.0.0.1;Port=55432;Database=$sourceDatabase;Username=$sourceUsername;Password=$targetPassword"
    $configLines = @(
        '# Migrate-DevelopmentDatabase.ps1 が生成。Git管理外。',
        "POSTGRES_DB=$sourceDatabase",
        "POSTGRES_USER=$sourceUsername",
        "POSTGRES_PASSWORD=$targetPassword",
        "ConnectionStrings__DefaultConnection=`"$targetConnectionString`""
    )
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $configFile) | Out-Null
    [IO.File]::WriteAllLines($configFile, $configLines, [Text.UTF8Encoding]::new($false))
}
else {
    $existingConfigText = Get-Content -LiteralPath $configFile -Raw
    if ($existingConfigText.Contains('CHANGE_THIS_')) {
        throw "$configFile に例示用パスワードが残っています。実値へ変更してください。"
    }
    $targetValues = @{}
    foreach ($line in Get-Content -LiteralPath $configFile) {
        $parts = $line.Trim().Split('=', 2)
        if ($parts.Count -eq 2 -and -not $parts[0].StartsWith('#')) {
            $targetValues[$parts[0]] = $parts[1].Trim().Trim('"')
        }
    }
    foreach ($requiredKey in @('POSTGRES_DB', 'POSTGRES_USER', 'POSTGRES_PASSWORD', 'ConnectionStrings__DefaultConnection')) {
        if (-not $targetValues.ContainsKey($requiredKey) -or [string]::IsNullOrWhiteSpace($targetValues[$requiredKey])) {
            throw "$configFile の $requiredKey が未設定です。"
        }
    }
    $targetConnectionString = $targetValues['ConnectionStrings__DefaultConnection']
}

New-Item -ItemType Directory -Force -Path $dataDirectory, $backupDirectory | Out-Null
if ($PrepareOnly) {
    Write-Host "Git管理外の開発DB設定とruntimeディレクトリを準備しました: $configFile"
    return
}
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw '開発DB設定は準備済みですが、Dockerが見つかりません。Docker Desktopをインストールしてから再実行してください。接続先のLocal設定は変更していません。'
}
Invoke-DockerCompose -Arguments @('up', '-d', '--wait')

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$dumpFileName = "source-before-local-move-$timestamp.dump"
$dumpContainerPath = "/backups/$dumpFileName"

# 移行元パスワードはファイルやログへ書かず、pg_dumpプロセスの標準入力から一時環境変数へ渡す。
$sourcePassword | & docker compose --env-file $configFile -f $composeFile exec -T postgres `
    sh -ceu 'IFS= read -r PGPASSWORD; export PGPASSWORD; exec pg_dump -Fc --no-owner --no-privileges --host "$1" --port "$2" --username "$3" --dbname "$4" --file "$5"' `
    -- $sourceHost $sourcePort $sourceUsername $sourceDatabase $dumpContainerPath
if ($LASTEXITCODE -ne 0) {
    throw '移行元DBのバックアップに失敗しました。接続先のLocal設定は変更していません。'
}

$target = [System.Data.Common.DbConnectionStringBuilder]::new()
$target.set_ConnectionString($targetConnectionString)
$targetDatabase = Get-ConnectionValue -Builder $target -Names @('Database', 'Initial Catalog') -Required
$targetUsername = Get-ConnectionValue -Builder $target -Names @('Username', 'User ID', 'UserId') -Required

Invoke-DockerCompose -Arguments @(
    'exec', '-T', 'postgres', 'pg_restore', '--clean', '--if-exists',
    '--no-owner', '--no-privileges', '--exit-on-error',
    '-U', $targetUsername, '-d', $targetDatabase, $dumpContainerPath)
Invoke-DockerCompose -Arguments @(
    'exec', '-T', 'postgres', 'psql', '-U', $targetUsername, '-d', $targetDatabase,
    '-v', 'ON_ERROR_STOP=1', '-c', 'select 1;')

$settingsBackup = Join-Path $backupDirectory "appsettings.Development.Local.$timestamp.json"
Copy-Item -LiteralPath $localSettingsFile -Destination $settingsBackup
$localSettings.ConnectionStrings.DefaultConnection = $targetConnectionString
$updatedJson = $localSettings | ConvertTo-Json -Depth 20
[IO.File]::WriteAllText($localSettingsFile, $updatedJson + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))

Write-Host '開発DBをプロジェクト配下へ複製し、F5の接続先を127.0.0.1:55432へ切り替えました。'
Write-Host "移行元バックアップ: $(Join-Path $backupDirectory $dumpFileName)"
Write-Host "旧Local設定: $settingsBackup"
