<#
.SYNOPSIS
WindBot Ignite(External/windbot 서브모듈)에서 AI 본체 소스만 Assets/ThirdParty/WindBot/Upstream으로 복사한다.

.DESCRIPTION
유니티는 Assets 밖의 소스를 컴파일하지 않고, 폴더 안 파일을 골라 뺄 수도 없다. 그래서 필요한 파일만 고쳐 쓰지 않고 그대로 복사한다.
- 복사하는 것: ExecutorBase의 Game 폴더(GameAI, Executor, DefaultExecutor, AIUtil, 필드 모델 등), Card·NamedCard, 열거형, LICENSE, COPYING
- 복사하지 않는 것 (Assets/ThirdParty/WindBot/Replacements에 대신할 파일이 있다):
  NamedCardsManager.cs, CardsManager.cs (Mono.Data.Sqlite로 카드 DB를 읽음 → 이 프로젝트의 CardDatabase로 채운다)
  Dialogs.cs (대사 JSON 파일을 읽음 → 대사 없이 둔다)
- 네트워크 처리(GameBehavior, YGOSharp.Network), 실행 파일(Program), 덱별 Executor는 쓰지 않는다.
  네트워크 처리 대신 Assets/Scripts/AI/WindBotPlayer.cs가 엔진 메시지를 직접 넣는다.

이미 있는 파일은 덮어쓰고(.meta는 그대로 두어 유니티 GUID를 유지), 목록에서 빠진 파일은 .meta와 함께 지운다.
서브모듈 버전을 올린 뒤 다시 실행하고, THIRD_PARTY_NOTICES.md의 버전도 고친다.

.EXAMPLE
powershell -ExecutionPolicy Bypass -File C:\Dev\DuelRoguelike\tools\import-windbot.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Root     = Split-Path -Parent $PSScriptRoot
$Source   = Join-Path $Root 'External\windbot'
$DestRoot = Join-Path $Root 'Assets\ThirdParty\WindBot'
$Dest     = Join-Path $DestRoot 'Upstream'

# ExecutorBase 안에서 복사하지 않는 파일 (대신할 파일이 Replacements에 있다)
$Excluded = @(
    'Game\Dialogs.cs',
    'YGOSharp.OCGWrapper\CardsManager.cs',
    'YGOSharp.OCGWrapper\NamedCardsManager.cs'
)

function Write-Step([string]$Message) { Write-Host "==> $Message" -ForegroundColor Cyan }

$executorBase = Join-Path $Source 'ExecutorBase'
if (-not (Test-Path (Join-Path $executorBase 'Game\GameAI.cs'))) {
    throw "WindBot 소스가 없습니다: $Source`n먼저 실행하세요: git submodule update --init External/windbot"
}
$commit = (git -C $Source rev-parse HEAD).Trim()

Write-Step "WindBot $($commit.Substring(0, 7)) 에서 소스 목록 만드는 중"
$files = @()
foreach ($dir in 'Game', 'YGOSharp.OCGWrapper', 'YGOSharp.OCGWrapper.Enums') {
    foreach ($file in Get-ChildItem (Join-Path $executorBase $dir) -Recurse -Filter '*.cs' -File) {
        $relative = $file.FullName.Substring($executorBase.Length + 1)
        if ($Excluded -notcontains $relative) { $files += $relative }
    }
}
$files = $files | Sort-Object

Write-Step "$($files.Count)개 파일을 $Dest 로 복사하는 중"
New-Item -ItemType Directory -Force $Dest | Out-Null
foreach ($relative in $files) {
    $target = Join-Path $Dest $relative
    New-Item -ItemType Directory -Force (Split-Path -Parent $target) | Out-Null
    Copy-Item (Join-Path $executorBase $relative) $target -Force
}

# 목록에서 빠진 파일 지우기
foreach ($existing in Get-ChildItem $Dest -Recurse -Filter '*.cs' -File) {
    $relative = $existing.FullName.Substring($Dest.Length + 1)
    if ($files -notcontains $relative) {
        Write-Step "지움: $relative"
        Remove-Item $existing.FullName -Force
        if (Test-Path "$($existing.FullName).meta") { Remove-Item "$($existing.FullName).meta" -Force }
    }
}

# 라이선스 (AGPL-3.0-or-later 본문과, 원래 MIT였던 IceYGO WindBot 고지)
Copy-Item (Join-Path $Source 'LICENSE') (Join-Path $DestRoot 'LICENSE.txt') -Force
Copy-Item (Join-Path $Source 'COPYING') (Join-Path $DestRoot 'COPYING.txt') -Force
Set-Content -Path (Join-Path $Dest 'UPSTREAM.txt') -Encoding UTF8 -Value @(
    'https://github.com/ProjectIgnis/windbot',
    "commit $commit",
    'tools/import-windbot.ps1 로 ExecutorBase에서 복사했다. 이 폴더의 파일은 고치지 않는다.'
)

Write-Step "완료: $($files.Count)개 파일 (WindBot $commit)"
