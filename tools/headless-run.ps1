<#
.SYNOPSIS
유니티 에디터 없이 로그라이크 런을 여러 개 처음부터 끝까지 돌려, 런 규칙(보상 후보·골드·봉인·LP·보충·종료)과 듀얼이 문제없는지 확인한다.

.DESCRIPTION
1. 어셈블리를 컴파일하고 불러온다 (tools/headless-common.ps1, headless-duel.ps1과 같다).
2. 등급 표(Assets/StreamingAssets/Data/rarity.txt)를 읽고 확인한다: 상대 덱의 모든 카드에 등급이 있는지, 덱마다 SR·UR이 있는지.
3. 엔진 없이 런 규칙 시험(RunRuleSelfTest)을 돌린다: 정해 둔 승패로 런을 진행해 기대값과 비교한다.
4. 런을 돌린다 (HeadlessRunTest). 플레이어 0(-Player)이 시작 덱(-StartDeck)으로 런을 하고, 층마다 WindBot이 무작위 상대 덱을 맡는다.
   시작 덱마다 시드 FirstSeed ~ FirstSeed+Runs-1의 런을 하나씩 돌린다. 승리 보상(후보 3자리)과 보충할 카드는 무작위로 고른다.
통과 조건 (종료 코드 0):
- 등급 표 문제 0, 규칙 시험 실패 0
- 모든 런이 런 종료 조건(메인 덱을 40장으로 채울 수 없음)으로 끝남 (듀얼 수 상한에 걸리지 않음)
- 모든 듀얼이 끝나고 엔진 오류·예외·대체 응답·거절된 응답·해석하지 않은 메시지 0
- 단계마다 다시 계산한 런 규칙 점검 위반 0
- 같은 시드·같은 결과로 런 규칙만 다시 돌린 이벤트가 같음, 런의 첫 듀얼을 엔진으로 다시 돌린 결과가 같음
- 저장: 단계마다 저장 파일로 쓰고 읽어 다시 쓴 글이 같음, 걸음마다 저장 파일을 읽은 런으로 이어 가도 끝 상태가 같음,
  듀얼 중에 쓴 저장 파일을 읽으면 그 듀얼이 LP 0 패배로 처리됨
보상 등급 분포, 골드, 봉인 장수, 듀얼 시작 LP별 승률 등은 보고만 한다.

PowerShell 7(pwsh)에서 실행한다.

.EXAMPLE
pwsh -File C:\Dev\DuelRoguelike\tools\headless-run.ps1 -Runs 10 -Player WindBot -StartDeck All

.EXAMPLE
pwsh -File C:\Dev\DuelRoguelike\tools\headless-run.ps1 -CheckOnly

.EXAMPLE
pwsh -File C:\Dev\DuelRoguelike\tools\headless-run.ps1 -Runs 1 -FirstSeed 3 -PrintRunSeed 3 -StartDeck Joey -Player Auto
#>
[CmdletBinding()]
param(
    # 시작 덱마다 돌릴 런 수
    [int]$Runs = 10,
    [int]$FirstSeed = 1,
    [ValidateSet('Auto', 'WindBot', 'Interaction')]
    [string]$Player = 'WindBot',
    # 시작 덱: Yugi, Kaiba, Joey, Pegasus, All (쉼표로 여러 개)
    [string[]]$StartDeck = @('All'),
    # 런 하나의 듀얼 수 상한 (넘으면 끝나지 않은 런으로 실패)
    [int]$MaxDuels = 400,
    [int]$MaxTurns = 100,
    # 이 시드의 런은 런 이벤트를 모두 출력한다
    [int]$PrintRunSeed = -1,
    # 비워 두면 Assets\StreamingAssets\Data\rarity.txt
    [string]$RarityFile,
    # 등급 표 확인과 규칙 시험(엔진 없이)만 한다
    [switch]$CheckOnly,
    # WindBot이 상대의 비공개 정보를 보게 한다
    [switch]$NoHide,
    [string]$UnityEditor
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
[Console]::OutputEncoding = [Text.Encoding]::UTF8

. (Join-Path $PSScriptRoot 'headless-common.ps1')
$cardData = Initialize-Headless $UnityEditor
$db = $cardData.Database
$failed = $false

Write-Step '등급 표 확인'
if (-not $RarityFile) { $RarityFile = Join-Path $Root 'Assets\StreamingAssets\Data\rarity.txt' }
$rarities = [DuelRoguelike.Run.RarityTable]::Load($RarityFile)
$starters = [DuelRoguelike.Engine.Data.StarterDecks]::All
Write-Host "    $RarityFile : 카드 $($rarities.Count)장"
foreach ($deck in $starters) {
    $c = $rarities.CountByRarity($deck)
    Write-Host ("    {0}: N {1}, R {2}, SR {3}, UR {4}, FR {5}" -f $deck.Name, $c[0], $c[1], $c[2], $c[3], $c[4])
}
$rarityProblems = $rarities.Check($starters, $db)
foreach ($p in $rarityProblems) { Write-Host "      $p" -ForegroundColor Red }
if ($rarityProblems.Count -gt 0) { $failed = $true } else { Write-Host '    문제 없음' }

Write-Step '런 규칙 시험 (엔진 없이)'
$checks = 0
$selfFailures = [DuelRoguelike.Dev.RunRuleSelfTest]::Run($db, $rarities, [ref]$checks)
Write-Host "    확인 $checks 건, 실패 $($selfFailures.Count)건"
foreach ($f in $selfFailures) { Write-Host "      $f" -ForegroundColor Red }
if ($selfFailures.Count -gt 0) { $failed = $true }

if ($CheckOnly) {
    if ($failed) { Write-Host '==> 실패' -ForegroundColor Red; exit 1 }
    Write-Step '통과'
    exit 0
}

$log = [Action[string]] { param($line) Write-Host $line }
$duelRun = [DuelRoguelike.Dev.HeadlessDuelRun]::new($cardData, $log)

Write-Step '스타터 덱 레시피 확인'
foreach ($recipe in $starters) {
    $problems = $duelRun.CheckRecipe($recipe)
    foreach ($p in $problems) { Write-Host "      $($recipe.Id): $p" -ForegroundColor Red }
    if ($problems.Count -gt 0) { $failed = $true }
}

$decks = @(Resolve-Decks $StartDeck 'StartDeck')
$test = [DuelRoguelike.Dev.HeadlessRunTest]::new($duelRun, $rarities, $log)
Write-Step "런 $($Runs * $decks.Count)개 (시작 덱 $(($decks | ForEach-Object Id) -join ', ') × 시드 $FirstSeed~$($FirstSeed + $Runs - 1)), 플레이어 = $Player, 상대 = WindBot"
$watch = [Diagnostics.Stopwatch]::StartNew()
$r = $test.Run($Runs, $FirstSeed, [DuelRoguelike.Dev.HeadlessPlayerKind]$Player,
    [DuelRoguelike.Engine.Data.DeckRecipe[]]$decks, $MaxDuels, $MaxTurns, $PrintRunSeed, (-not $NoHide))
$watch.Stop()

function Format-Stats($values) {
    $list = @($values)
    if ($list.Count -eq 0) { return '-' }
    $m = $list | Measure-Object -Minimum -Maximum -Average
    "최소 $($m.Minimum), 평균 $([math]::Round($m.Average, 1)), 최대 $($m.Maximum)"
}
function Percent([double]$a, [double]$b) { if ($b -le 0) { '-' } else { "$([math]::Round(100 * $a / $b, 1))%" } }
$rarityNames = 'N', 'R', 'SR', 'UR', 'FR'

Write-Step "결과 ($([int]$watch.Elapsed.TotalSeconds)초)"
Write-Host "    런 $($r.Runs)개: 런 종료 $($r.Ended), 듀얼 수 상한 $($r.Capped), 중단 $($r.Aborted)"
Write-Host "    듀얼 $($r.Duels)판: 승 $($r.Wins), 패 $($r.Losses), 무 $($r.Draws) (승률 $(Percent $r.Wins $r.Duels))"
Write-Host "    런 길이(듀얼): $(Format-Stats $r.RunDuels)"
Write-Host "    마지막 층: $(Format-Stats $r.FinalFloors)"
foreach ($pair in $r.ByStartDeck.GetEnumerator()) {
    $v = $pair.Value
    Write-Host "      시작 덱 $($pair.Key): 런 $($v[0]), 듀얼 $($v[1]), 승률 $(Percent $v[2] $v[1]), 평균 마지막 층 $([math]::Round($v[3] / [math]::Max(1, $v[0]), 1))"
}
foreach ($pair in $r.ByOpponent.GetEnumerator()) {
    Write-Host "      상대 $($pair.Key): 듀얼 $($pair.Value[0]), 플레이어 승률 $(Percent $pair.Value[1] $pair.Value[0])"
}

$d = $r.Duel
Write-Host "    듀얼 점검: 끝난 판 $($d.Finished) / $($d.Duels), 엔진 오류 $($d.EngineErrors), 예외 $($d.Exceptions), 거절 $($d.Retries[0]) / $($d.Retries[1]), 해석하지 않은 메시지 $($d.UnparsedTypes.Count)종"
Write-Host "      WindBot: 직접 답함 $($d.WindBotAnswered), 대신 답함 $($d.WindBotFallbacks), 모델 맞춤 $($d.WindBotResyncs)칸, 메시지 처리 예외 $($d.WindBotMessageErrors), 상대 비공개 카드 $($d.WindBotKnownSecrets)"
if ($Player -eq 'Interaction') {
    Write-Host "      화면 조작 모델: 답함 $($d.InteractionAnswered), 대신 답함 $($d.InteractionFallbacks)"
}
foreach ($p in $d.WindBotProblems) { Write-Host "      $p" -ForegroundColor Yellow }

$s = $r.Rules
Write-Host "    런 규칙 점검: 위반 $($s.ViolationCount)건"
foreach ($v in $s.Violations) { Write-Host "      $v" -ForegroundColor Red }
Write-Host '    승리 보상 (뽑힌 등급: 실제 / 확률표 기대값, 카드를 고름, 빈 자리(골드)를 고름, 후보가 3장보다 적음, 등급 대체(골드))'
for ($i = 0; $i -lt 5; $i++) {
    Write-Host ("      {0,-2}: {1} / {2:F1}, 카드 {3}, 빈 자리 {4}, 후보 부족 {5}, 대체 {6}" -f $rarityNames[$i], $s.Rolls[$i], $s.ExpectedRolls[$i], $s.Gained[$i], $s.GoldChosen[$i], $s.ShortOffers[$i], $s.Substituted[$i])
}
Write-Host "    골드: 모든 런 합 $($s.Gold) (런 평균 $([math]::Round($s.Gold / [math]::Max(1, $r.Runs), 1)))"
Write-Host "    봉인: 메인 덱 $($s.MainSealed)장, 엑스트라 덱 $($s.ExtraSealed)장"
$chainTotal = ($s.ExtraChain | Measure-Object -Sum).Sum
if ($chainTotal -gt 0) {
    Write-Host "      엑스트라 덱이 2장 이상일 때 패배 $chainTotal 번: 0장 $($s.ExtraChain[0]), 1장 $($s.ExtraChain[1]), 2장 $($s.ExtraChain[2]), 3장 이상 $($s.ExtraChain[3]) (1장 이상 $(Percent ($chainTotal - $s.ExtraChain[0]) $chainTotal), 기대 20%)"
}
Write-Host "    LP: 보스 층 직전 8000으로 맞춤 $($s.BossFloorAdjusts)번, 보스 격파 $($s.BossClears)번, 패배 회복 $($s.LossHeals)번, 8000보다 높은 LP로 시작한 듀얼 $($s.DuelsAboveFullLp)판"
Write-Host "    보충: $($s.RefillsRequired)번, $($s.RefillCards)장 / 런 종료 기록 $($s.RunsEnded)번"
Write-Host '    듀얼 시작 LP별 승률'
for ($i = 0; $i -lt 5; $i++) {
    Write-Host ("      {0,-10}: 듀얼 {1}, 승률 {2}" -f [DuelRoguelike.Dev.RunRuleStats]::LpBucketNames[$i], $s.LpBucketDuels[$i], (Percent $s.LpBucketWins[$i] $s.LpBucketDuels[$i]))
}
Write-Host "    재현: 런 규칙 다시 돌리기 불일치 $($r.RuleReplayMismatches), 첫 듀얼 다시 돌리기 불일치 $($r.EngineReplayMismatches) (다시 돌린 듀얼 $($r.ReplayDuel.Duels)판, 점검 통과 $($r.ReplayDuel.Ok))"
Write-Host "    저장: 쓰고 읽어 다시 쓰기 $($r.SaveRoundTrips)번 중 불일치 $($r.SaveRoundTripMismatches), 걸음마다 저장을 읽어 이어 간 런 불일치 $($r.SaveReplayMismatches), 듀얼 중 저장을 읽어 패배 처리 $($r.ForfeitChecks)번 중 불일치 $($r.ForfeitMismatches)"
foreach ($p in $r.Problems) { Write-Host "      $p" -ForegroundColor Red }

if ($failed -or -not $r.Ok) { Write-Host '==> 실패' -ForegroundColor Red; exit 1 }
Write-Step '통과'
exit 0
