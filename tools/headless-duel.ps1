<#
.SYNOPSIS
유니티 에디터 없이 자동 대전을 여러 판 돌려, 엔진 연결·메시지 해석·상대 AI(WindBot)가 끝까지 문제없는지 확인한다.

.DESCRIPTION
1. ProjectSettings/ProjectVersion.txt의 유니티 버전으로 설치된 에디터를 찾는다 (Unity Hub 기본 경로).
2. (tools/headless-common.ps1) 에디터에 들어 있는 C# 컴파일러(Roslyn)로 어셈블리 정의(asmdef)와 같은 단위로 컴파일한다:
   DuelRoguelike.Engine → WindBot.Ignite → DuelRoguelike.AI → DuelRoguelike.Run → DuelRoguelike.UI → DuelRoguelike.Dev
   (.NET Standard 2.1, 유니티 엔진 DLL 참조, 에디터와 같이 DEBUG 정의). 결과는 tools/.cache/headless에 둔다.
3. ocgcore.dll, sqlite3.dll(Assets/Plugins/x86_64)을 같은 폴더에 복사하고, PowerShell에서 불러 HeadlessDuelRun을 돌린다.
4. 스타터 덱 4개 레시피(StarterDecks)가 덱 규칙에 맞고 카드 스크립트가 모두 있는지 먼저 확인한다.
덱은 -Deck0(플레이어 0), -Deck1(플레이어 1)로 고른다: Yugi, Kaiba, Joey, Pegasus, All.
쉼표로 여러 개를 주면 모든 조합을 차례로 돌린다 (All,All이면 4×4 = 16조합).
모든 조합에서 모든 판이 끝나고 엔진 오류·예외·대체 응답(화면 조작 모델, WindBot)·엔진이 거절한 응답·해석하지 않은 메시지가 0건이면 종료 코드 0이다.
화면에서 사람이 고를 수 없는 선택(순서 정하기, 카운터 분배, 카드 선언)은 통과 조건에 넣지 않고 따로 보고한다.

먼저 tools/build-ocgcore.ps1, tools/fetch-sqlite.ps1로 네이티브 DLL을 만들어 둔다.
PowerShell 7(pwsh)에서 실행한다.

.EXAMPLE
pwsh -File C:\Dev\DuelRoguelike\tools\headless-duel.ps1 -Duels 20 -Player0 Auto -Player1 WindBot

.EXAMPLE
pwsh -File C:\Dev\DuelRoguelike\tools\headless-duel.ps1 -Duels 50 -Player0 Interaction -Player1 WindBot -Deck0 All -Deck1 All

.EXAMPLE
pwsh -File C:\Dev\DuelRoguelike\tools\headless-duel.ps1 -Duels 1 -FirstSeed 5 -PrintLogSeed 5 -Deck0 Joey -Deck1 Pegasus
#>
[CmdletBinding()]
param(
    [int]$Duels = 20,
    [int]$FirstSeed = 1,
    [ValidateSet('Auto', 'WindBot', 'Interaction')]
    [string]$Player0 = 'Auto',
    [ValidateSet('Auto', 'WindBot')]
    [string]$Player1 = 'WindBot',
    # 플레이어 0·1의 덱: Yugi, Kaiba, Joey, Pegasus, All (쉼표로 여러 개, 모든 조합을 돌린다)
    [string[]]$Deck0 = @('Yugi'),
    [string[]]$Deck1 = @('Yugi'),
    # 이 시드의 듀얼은 로그를 모두 출력한다 (WindBot 내부 로그 포함)
    [int]$PrintLogSeed = -1,
    # 무한 반복을 잡기 위한 상한. 스타터 덱은 수비적인 판이 길어져 60턴을 넘기도 한다 (확인한 최장 64턴).
    [int]$MaxTurns = 100,
    # WindBot이 상대의 비공개 정보를 보게 한다 (가리기 점검이 0보다 커지는지 확인용)
    [switch]$NoHide,
    # 비워 두면 C:\Program Files\Unity\Hub\Editor\<프로젝트 버전>\Editor
    [string]$UnityEditor
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
[Console]::OutputEncoding = [Text.Encoding]::UTF8

. (Join-Path $PSScriptRoot 'headless-common.ps1')
$cardData = Initialize-Headless $UnityEditor

$log = [Action[string]] { param($line) Write-Host $line }
$run = [DuelRoguelike.Dev.HeadlessDuelRun]::new($cardData, $log)

Write-Step '스타터 덱 레시피 확인'
$recipeProblems = 0
foreach ($recipe in [DuelRoguelike.Engine.Data.StarterDecks]::All) {
    $problems = $run.CheckRecipe($recipe)
    Write-Host "    $($recipe.Id) ($($recipe.Name)): $($recipe.CardCount)장, 문제 $($problems.Count)건"
    foreach ($p in $problems) { Write-Host "      $p" -ForegroundColor Red }
    $recipeProblems += $problems.Count
}

$decks0 = Resolve-Decks $Deck0 'Deck0'
$decks1 = Resolve-Decks $Deck1 'Deck1'

function Format-Counts($dict) { ($dict.GetEnumerator() | Sort-Object Value -Descending | ForEach-Object { "$($_.Key) $($_.Value)" }) -join ', ' }
function Sum-Counts($dict, [string]$keyPrefix = '') {
    $sum = 0
    foreach ($pair in $dict.GetEnumerator()) { if ("$($pair.Key)".StartsWith($keyPrefix)) { $sum += $pair.Value } }
    $sum
}

$results = @()
$total = [Diagnostics.Stopwatch]::StartNew()
foreach ($d0 in $decks0) {
    foreach ($d1 in $decks1) {
        Write-Step "자동 대전 $Duels 판 (시드 $FirstSeed~$($FirstSeed + $Duels - 1)): 플레이어 0 = $Player0 ($($d0.Name)), 플레이어 1 = $Player1 ($($d1.Name))"
        $watch = [Diagnostics.Stopwatch]::StartNew()
        $r = $run.Run($Duels, $FirstSeed, $PrintLogSeed, $MaxTurns,
            [DuelRoguelike.Dev.HeadlessPlayerKind]$Player0, [DuelRoguelike.Dev.HeadlessPlayerKind]$Player1, (-not $NoHide), $d0, $d1)
        $watch.Stop()
        $results += $r

        Write-Host "    결과 ($([int]$watch.Elapsed.TotalSeconds)초): 끝난 판 $($r.Finished) / $($r.Duels), 평균 $([math]::Round($r.TotalTurns / [math]::Max(1, $r.Duels), 1))턴"
        Write-Host "    승패: $(Format-Counts $r.Winners)"
        Write-Host "    엔진 오류 $($r.EngineErrors), 예외 $($r.Exceptions), 엔진이 거절한 응답 $($r.Retries[0]) / $($r.Retries[1]) $(Format-Counts $r.RetryTypes)"
        Write-Host "    메시지마다 필드: 배치 끝 비교 $($r.StateChecks)번, 엔진과 다름 $($r.StateMismatches)번 $(Format-Counts $r.StateMismatchKinds)"
        Write-Host "    소환 $($r.Summons[0]) / $($r.Summons[1]), 효과 발동 $($r.Activations[0]) / $($r.Activations[1]), 공격 $($r.Attacks[0]) / $($r.Attacks[1])  (플레이어 0 / 1)"
        if ($Player0 -eq 'WindBot' -or $Player1 -eq 'WindBot') {
            Write-Host "    WindBot: 직접 답함 $($r.WindBotAnswered), 대신 답함 $($r.WindBotFallbacks), 모델 맞춤 $($r.WindBotResyncs)칸, 메시지 처리 예외 $($r.WindBotMessageErrors)"
            Write-Host "    가리기 $(if ($NoHide) { '끔' } else { '켬' }): WindBot이 알고 있던 상대 손패 비공개 카드 $($r.WindBotKnownSecrets)장 (응답마다 센 누적)"
            foreach ($problem in $r.WindBotProblems) { Write-Host "      $problem" -ForegroundColor Yellow }
        }
        if ($Player0 -eq 'Interaction') {
            Write-Host "    화면 조작 모델: 답함 $($r.InteractionAnswered) (칸 고르기 $($r.InteractionPlaces), 순서 정하기 $($r.InteractionSorts)), 대신 답함 $($r.InteractionFallbacks) $(Format-Counts $r.FallbackReasons)"
        }
        if ($r.ScreenlessChoices.Count -gt 0) { Write-Host "    화면 없이 정한 선택 (플레이어 0): $(Format-Counts $r.ScreenlessChoices)" -ForegroundColor Yellow }
        if ($r.UnparsedTypes.Count -gt 0) { Write-Host "    해석하지 않은 메시지: $(Format-Counts $r.UnparsedTypes)" -ForegroundColor Yellow }
        if (-not $r.Ok) { Write-Host '    → 실패' -ForegroundColor Red }
    }
}
$total.Stop()

if ($results.Count -gt 1) {
    Write-Step "조합별 요약 ($([int]$total.Elapsed.TotalSeconds)초)"
    $results | ForEach-Object {
        [pscustomobject]@{
            '덱 0'       = $_.Deck0
            '덱 1'       = $_.Deck1
            '끝난 판'    = "$($_.Finished)/$($_.Duels)"
            '승 0:1'     = "$(Sum-Counts $_.Winners '플레이어 0'):$(Sum-Counts $_.Winners '플레이어 1')"
            '오류'       = $_.EngineErrors
            '예외'       = $_.Exceptions
            '거절'       = $_.Retries[0] + $_.Retries[1]
            '대체(화면)' = $_.InteractionFallbacks
            '대체(봇)'   = $_.WindBotFallbacks
            '미해석'     = Sum-Counts $_.UnparsedTypes
            '화면 없음'  = Sum-Counts $_.ScreenlessChoices
            '통과'       = $_.Ok
        }
    } | Format-Table -AutoSize | Out-String -Width 200 | Write-Host
}

Write-Step '카드별 확인 범위 (일반 몬스터 제외, 통과 조건에는 넣지 않음)'
$coverage = $run.CoverageReport([DuelRoguelike.Dev.HeadlessDuelRun+Result[]]$results,
    [DuelRoguelike.Engine.Data.DeckRecipe[]]$decks0, [DuelRoguelike.Engine.Data.DeckRecipe[]]$decks1)
if ($coverage.Count -eq 0) { Write-Host '    효과 카드가 모두 양쪽 플레이어에서 한 번 이상 발동·소환됐다' }
foreach ($line in $coverage) { Write-Host "    $line" -ForegroundColor $(if ($line.StartsWith('  ')) { 'Yellow' } else { 'Gray' }) }

if ($recipeProblems -gt 0) { Write-Host "==> 실패: 레시피 문제 $recipeProblems 건" -ForegroundColor Red; exit 1 }
if (@($results | Where-Object { -not $_.Ok }).Count -eq 0) {
    Write-Step '통과'
    exit 0
}
Write-Host '==> 실패' -ForegroundColor Red
exit 1
