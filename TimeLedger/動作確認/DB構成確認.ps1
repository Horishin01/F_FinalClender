[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path -Parent $PSScriptRoot
$databaseDirectory = Join-Path $projectDirectory 'database'
$programFile = Join-Path $projectDirectory 'Program.cs'
$failures = [Collections.Generic.List[string]]::new()

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        $failures.Add($Message)
    }
}

foreach ($environmentName in @('production')) {
    $composeFile = Join-Path $databaseDirectory "compose.$environmentName.yaml"
    $exampleFile = Join-Path $databaseDirectory "config\$environmentName.env.example"
    Assert-True (Test-Path -LiteralPath $composeFile -PathType Leaf) "$composeFile がありません。"
    Assert-True (Test-Path -LiteralPath $exampleFile -PathType Leaf) "$exampleFile がありません。"

    if (Test-Path -LiteralPath $composeFile -PathType Leaf) {
        $composeText = Get-Content -LiteralPath $composeFile -Raw
        Assert-True $composeText.Contains('image: postgres:16.15') "$composeFile のPostgreSQLバージョンが固定されていません。"
        Assert-True $composeText.Contains("source: ./runtime/$environmentName/data") "$composeFile のDB本体がプロジェクト配下ではありません。"
        Assert-True $composeText.Contains("source: ./runtime/$environmentName/backups") "$composeFile のバックアップ先がプロジェクト配下ではありません。"
        Assert-True $composeText.Contains('127.0.0.1:') "$composeFile がDBポートをloopbackに限定していません。"
        Assert-True $composeText.Contains('pg_isready') "$composeFile にhealthcheckがありません。"
    }

    if (Test-Path -LiteralPath $exampleFile -PathType Leaf) {
        $exampleText = Get-Content -LiteralPath $exampleFile -Raw
        Assert-True $exampleText.Contains('CHANGE_THIS_') "$exampleFile に実パスワードを置かないでください。"
        Assert-True $exampleText.Contains('ConnectionStrings__DefaultConnection=') "$exampleFile にアプリ接続設定がありません。"
    }
}

foreach ($scriptFile in @(
        (Join-Path $databaseDirectory 'scripts\Database.ps1'))) {
    $tokens = $null
    $parseErrors = $null
    [Management.Automation.Language.Parser]::ParseFile($scriptFile, [ref]$tokens, [ref]$parseErrors) | Out-Null
    Assert-True ($parseErrors.Count -eq 0) "$scriptFile にPowerShell構文エラーがあります。"
}

$bashScript = Join-Path $databaseDirectory 'scripts\database.sh'
Assert-True (Test-Path -LiteralPath $bashScript -PathType Leaf) "$bashScript がありません。"

$ignoreFile = Join-Path (Split-Path -Parent $projectDirectory) '.gitignore'
$ignoreText = Get-Content -LiteralPath $ignoreFile -Raw
Assert-True $ignoreText.Contains('TimeLedger/database/config/*.env') 'DBのenvファイルがGit除外されていません。'
Assert-True $ignoreText.Contains('TimeLedger/database/runtime/') 'DB物理データとバックアップがGit除外されていません。'
Assert-True $ignoreText.Contains('TimeLedger/timeledger.db') '開発用SQLite DBがGit除外されていません。'
Assert-True $ignoreText.Contains('TimeLedger/timeledger.db-wal') 'SQLite WALファイルがGit除外されていません。'
Assert-True $ignoreText.Contains('TimeLedger/timeledger.db-shm') 'SQLite SHMファイルがGit除外されていません。'

# development-local-sqlite-path
$programText = Get-Content -LiteralPath $programFile -Raw
Assert-True $programText.Contains('Path.Combine(builder.Environment.ContentRootPath, "timeledger.db")') 'development-local-sqlite-path: 開発DBがTimeLedger直下のtimeledger.dbに設定されていません。'

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    throw "DB構成確認に$($failures.Count)件失敗しました。"
}

Write-Host 'DB構成確認: 開発用SQLiteのTimeLedger直下保存、Production DB構成、Git除外、PowerShell構文を確認しました。'
