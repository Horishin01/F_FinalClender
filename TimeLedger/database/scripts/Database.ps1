[CmdletBinding()]
param(
    [ValidateSet('Production')]
    [string]$Environment = 'Production',

    [ValidateSet('Start', 'Stop', 'Status', 'Backup', 'Restore')]
    [string]$Action = 'Start',

    [string]$BackupFile,

    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$databaseDirectory = Split-Path -Parent $PSScriptRoot
$environmentName = $Environment.ToLowerInvariant()
$composeFile = Join-Path $databaseDirectory "compose.$environmentName.yaml"
$configFile = Join-Path $databaseDirectory "config\$environmentName.env"
$runtimeDirectory = Join-Path $databaseDirectory "runtime\$environmentName"
$backupDirectory = Join-Path $runtimeDirectory 'backups'
$dataDirectory = Join-Path $runtimeDirectory 'data'

function Invoke-DockerCompose {
    param([string[]]$Arguments)

    & docker compose --env-file $configFile -f $composeFile @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose $($Arguments -join ' ') に失敗しました。"
    }
}

function Read-DatabaseEnvironment {
    $values = @{}
    foreach ($line in Get-Content -LiteralPath $configFile) {
        $trimmed = $line.Trim()
        if (-not $trimmed -or $trimmed.StartsWith('#')) {
            continue
        }

        $parts = $trimmed.Split('=', 2)
        if ($parts.Count -eq 2) {
            $values[$parts[0]] = $parts[1].Trim().Trim('"')
        }
    }

    return $values
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Dockerが見つかりません。Docker DesktopまたはDocker Engine + Compose pluginをインストールしてください。'
}

if (-not (Test-Path -LiteralPath $configFile -PathType Leaf)) {
    $exampleFile = "$configFile.example"
    throw "$configFile がありません。$exampleFile をコピーして秘密値を設定してください。"
}

$configText = Get-Content -LiteralPath $configFile -Raw
if ($configText.Contains('CHANGE_THIS_')) {
    throw "$configFile に例示用パスワードが残っています。実値へ変更してください。"
}

$databaseEnvironment = Read-DatabaseEnvironment
foreach ($requiredKey in @('POSTGRES_DB', 'POSTGRES_USER', 'POSTGRES_PASSWORD')) {
    if (-not $databaseEnvironment.ContainsKey($requiredKey) -or
        [string]::IsNullOrWhiteSpace($databaseEnvironment[$requiredKey])) {
        throw "$configFile の $requiredKey が未設定です。"
    }
}

New-Item -ItemType Directory -Force -Path $dataDirectory, $backupDirectory | Out-Null

switch ($Action) {
    'Start' {
        Invoke-DockerCompose -Arguments @('up', '-d', '--wait')
        Invoke-DockerCompose -Arguments @('ps')
    }
    'Stop' {
        Invoke-DockerCompose -Arguments @('down')
        Write-Host "停止しました。DB本体は $dataDirectory に保持されています。"
    }
    'Status' {
        Invoke-DockerCompose -Arguments @('ps')
    }
    'Backup' {
        Invoke-DockerCompose -Arguments @('up', '-d', '--wait')
        $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
        $fileName = "timeledger-$environmentName-$timestamp.dump"
        $containerPath = "/backups/$fileName"
        Invoke-DockerCompose -Arguments @(
            'exec', '-T', 'postgres', 'pg_dump', '-Fc', '--no-owner', '--no-privileges',
            '-U', $databaseEnvironment['POSTGRES_USER'],
            '-d', $databaseEnvironment['POSTGRES_DB'],
            '-f', $containerPath)
        Write-Host "バックアップを作成しました: $(Join-Path $backupDirectory $fileName)"
    }
    'Restore' {
        if (-not $Force) {
            throw '復元は既存DBを上書きします。内容を確認後、-Force を付けて再実行してください。'
        }
        if ([string]::IsNullOrWhiteSpace($BackupFile)) {
            throw 'Restoreには -BackupFile が必要です。'
        }

        $resolvedBackup = (Resolve-Path -LiteralPath $BackupFile).Path
        $resolvedBackupDirectory = (Resolve-Path -LiteralPath $backupDirectory).Path
        if (-not $resolvedBackup.StartsWith($resolvedBackupDirectory + [IO.Path]::DirectorySeparatorChar,
                [StringComparison]::OrdinalIgnoreCase)) {
            throw "復元ファイルは $backupDirectory の直下に置いてください。"
        }

        Invoke-DockerCompose -Arguments @('up', '-d', '--wait')

        $safetyTimestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
        $safetyFileName = "before-restore-$environmentName-$safetyTimestamp.dump"
        Invoke-DockerCompose -Arguments @(
            'exec', '-T', 'postgres', 'pg_dump', '-Fc', '--no-owner', '--no-privileges',
            '-U', $databaseEnvironment['POSTGRES_USER'],
            '-d', $databaseEnvironment['POSTGRES_DB'],
            '-f', "/backups/$safetyFileName")

        $containerBackup = "/backups/$([IO.Path]::GetFileName($resolvedBackup))"
        Invoke-DockerCompose -Arguments @(
            'exec', '-T', 'postgres', 'pg_restore', '--clean', '--if-exists',
            '--no-owner', '--no-privileges', '--exit-on-error',
            '-U', $databaseEnvironment['POSTGRES_USER'],
            '-d', $databaseEnvironment['POSTGRES_DB'],
            $containerBackup)
        Write-Host "復元しました。復元直前バックアップ: $(Join-Path $backupDirectory $safetyFileName)"
    }
}
