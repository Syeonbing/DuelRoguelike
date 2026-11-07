<#
.SYNOPSIS
sqlite.org 공식 64비트 SQLite DLL을 받아 Assets/Plugins/x86_64/sqlite3.dll로 복사한다.

.DESCRIPTION
카드 DB(.cdb, SQLite 파일)를 읽는 데 쓴다. SQLite는 퍼블릭 도메인이다.
1. sqlite-dll-win-x64 압축 파일이 tools/.cache에 없으면 sqlite.org에서 받는다.
2. 해시를 확인한다. SHA3-256을 지원하는 PowerShell 7에서는 sqlite.org가 게시한 SHA3-256으로,
   Windows PowerShell 5.1에서는 같은 파일의 SHA256으로 확인한다.
3. sqlite3.dll을 유니티 플러그인 폴더로 복사한다.

유니티 에디터가 DLL을 불러온 뒤에는 에디터를 닫아야 다시 복사할 수 있다.

.EXAMPLE
powershell -ExecutionPolicy Bypass -File C:\Dev\DuelRoguelike\tools\fetch-sqlite.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Root      = Split-Path -Parent $PSScriptRoot
$PluginDir = Join-Path $Root 'Assets\Plugins\x86_64'
$CacheDir  = Join-Path $PSScriptRoot '.cache'

# https://www.sqlite.org/download.html
$SqliteVersion = '3.54.1'
$ZipName       = 'sqlite-dll-win-x64-3540100.zip'
$ZipUrl        = "https://www.sqlite.org/2026/$ZipName"
$ZipSize       = 1450919
$ZipSha3_256   = 'C2D09C8BABB9399277055C057C97A53F1FFD7C5F11D0C1F2118FFDFA39F2DAA4' # sqlite.org 게시 값
$ZipSha256     = 'F436F53AD8F965AF43F3817B42C99C0689E6787F6B7ACE208FE38DAB7FFC4971' # 위 SHA3-256과 맞는 파일에서 계산

function Write-Step([string]$Message) { Write-Host "==> $Message" -ForegroundColor Cyan }

function Test-ZipHash([string]$Path) {
    if ((Get-Item $Path).Length -ne $ZipSize) { return $false }
    $sha3Type = 'System.Security.Cryptography.SHA3_256' -as [type]
    if ($sha3Type -and $sha3Type::IsSupported) {
        $hash = [BitConverter]::ToString($sha3Type::HashData([IO.File]::ReadAllBytes($Path))).Replace('-', '')
        return $hash -eq $ZipSha3_256
    }
    return (Get-FileHash $Path -Algorithm SHA256).Hash -eq $ZipSha256
}

New-Item -ItemType Directory -Force $CacheDir | Out-Null
$zip = Join-Path $CacheDir $ZipName

if (-not (Test-Path $zip)) {
    Write-Step "SQLite $SqliteVersion 받는 중: $ZipUrl"
    Invoke-WebRequest -Uri $ZipUrl -OutFile $zip -UseBasicParsing
}
if (-not (Test-ZipHash $zip)) {
    Remove-Item $zip -Force
    throw "SQLite 압축 파일의 크기나 해시가 예상과 다릅니다. 파일을 지웠습니다. 다시 실행하세요."
}

Write-Step 'sqlite3.dll 꺼내는 중'
$extractDir = Join-Path $CacheDir "sqlite-$SqliteVersion"
Expand-Archive -Path $zip -DestinationPath $extractDir -Force
$dll = Join-Path $extractDir 'sqlite3.dll'
if (-not (Test-Path $dll)) { throw "압축 파일에 sqlite3.dll이 없습니다: $zip" }

New-Item -ItemType Directory -Force $PluginDir | Out-Null
try {
    Copy-Item $dll $PluginDir -Force
} catch {
    throw "sqlite3.dll을 복사하지 못했습니다. 유니티 에디터가 DLL을 잡고 있으면 에디터를 닫고 다시 실행하세요. ($($_.Exception.Message))"
}
Write-Step "완료: $(Join-Path $PluginDir 'sqlite3.dll') (SQLite $SqliteVersion)"
